using System;
using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Contamination
{
    /// <summary>
    /// 放射能汚染ゾーンの台帳と、土壌汚染グリッド(NaturalResourceManager.m_pollution)への適用/維持/除去。
    /// 基本設定は NuclearMeltdown 準拠（濃度255・50年で期限切れ・自然減衰対策の reassert・セーブ永続）。
    /// <b>汚水処理場では除染されない</b>。専用の「Decontamination facility」建物がゾーン付近で稼働している場合のみ、
    /// そのゾーンの濃度をゲーム内1か月あたり DecontaminationMonthlyFraction 相対除去し、恒久的に軽減する。
    /// すべて sim スレッドから呼ぶこと。
    /// </summary>
    public static class ContaminationManager
    {
        private static List<ContaminationZone> _zones = new List<ContaminationZone>();
        private static int _maintainCounter;
        private static long _lastMaintainTicks;
        private static readonly List<Vector3> _facilities = new List<Vector3>(); // 稼働中の除染施設の位置

        /// <summary>ゾーン台帳のスナップショット（セーブ用）。</summary>
        public static List<ContaminationZone> Zones
        {
            get { return new List<ContaminationZone>(_zones); }
        }

        /// <summary>レベル切替時に呼ぶ（メモリ上の台帳を破棄。土壌汚染自体はゲームのセーブに含まれる）。</summary>
        public static void Reset()
        {
            _zones = new List<ContaminationZone>();
            _maintainCounter = 0;
            _lastMaintainTicks = 0;
            _facilities.Clear();
        }

        /// <summary>ロード時に台帳を差し替え、各ゾーンをグリッドへ再適用する。</summary>
        public static void ReplaceAll(List<ContaminationZone> zones)
        {
            _zones = zones ?? new List<ContaminationZone>();
            for (int i = 0; i < _zones.Count; i++) ReassertZone(_zones[i]);
        }

        /// <summary>着弾時に汚染ゾーンを追加してグリッドへ書き込む。radius&lt;=0 は無視（空中爆発など）。</summary>
        public static void AddZone(ContaminationZone zone)
        {
            if (zone.Radius <= 0f) return;
            if (zone.Radius > ModConfig.MaxContaminationRadius)
            {
                zone = new ContaminationZone(zone.CenterX, zone.CenterZ,
                    ModConfig.MaxContaminationRadius, zone.StartTicks, zone.Intensity);
            }
            _zones.Add(zone);
            ReassertZone(zone);
            ModConfig.Log("Contamination zone added: r=" + zone.Radius + "m at ("
                + zone.CenterX + "," + zone.CenterZ + "), total=" + _zones.Count);
        }

        /// <summary>
        /// sim スレッドから毎tick呼ぶ（内部で間引き）。期限切れ消去、除染施設付近は濃度を相対除去、
        /// それ以外は reassert で維持する。汚水処理場では除染しない。
        /// </summary>
        public static void Maintain(long nowTicks)
        {
            if (++_maintainCounter < ModConfig.ContaminationMaintainInterval) return;
            _maintainCounter = 0;

            // 経過月は処理サイクル間で測る。ゾーンが無くても時刻は前進させる（新ゾーンに空白期間を課さない=P2対策）。
            double deltaMonths = _lastMaintainTicks == 0
                ? 0.0
                : ContaminationDecay.MonthsBetween(_lastMaintainTicks, nowTicks);
            _lastMaintainTicks = nowTicks;
            if (_zones.Count == 0) { _facilities.Clear(); return; }

            ScanFacilities();

            double decayFactor = ContaminationDecay.DecayFactor(deltaMonths, ModConfig.DecontaminationMonthlyFraction);

            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                ContaminationZone zone = _zones[i];

                if (ContaminationClock.HasExpired(zone.StartTicks, nowTicks, ModConfig.ContaminationExpiryYears))
                {
                    ClearZone(zone);
                    _zones.RemoveAt(i);
                    ModConfig.Log("Contamination zone expired (" + ModConfig.ContaminationExpiryYears + "y) and cleared");
                    continue;
                }

                if (decayFactor < 1.0 && IsDecontaminated(zone))
                {
                    // float 濃度に係数を掛け続けるので微小間隔でも端数が失われず着実に減衰する。
                    zone.Intensity = (float)(zone.Intensity * decayFactor);
                    if (zone.Intensity <= ModConfig.DecontaminationMinIntensity)
                    {
                        ClearZone(zone);
                        _zones.RemoveAt(i);
                        ModConfig.Log("Contamination zone decontaminated and removed");
                    }
                    else
                    {
                        _zones[i] = zone;
                        SetZone(zone); // 下げた濃度をグリッドへ反映（上書き）
                    }
                }
                else
                {
                    ReassertZone(zone); // 自然減衰対策で維持（現在の濃度に戻す）
                }
            }
        }

        /// <summary>float 濃度を土壌汚染セルの上限濃度(byte)へ丸める。</summary>
        private static byte ToByteIntensity(float intensity)
        {
            int v = (int)(intensity + 0.5f);
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }

        /// <summary>汚染を維持する（自然減衰で下がったセルを zone.Intensity まで引き上げる）。変化があった時だけ再描画。</summary>
        public static void ReassertZone(ContaminationZone zone)
        {
            List<CellDose> doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.ApplyDose(doses[i]);
            if (changed) RefreshZoneTexture(zone); // 定常状態(無変化)では再描画しない＝オーバーレイの点滅を防ぐ
        }

        /// <summary>汚染を上書き設定する（除染で下げた濃度を反映）。変化があった時だけ再描画。</summary>
        private static void SetZone(ContaminationZone zone)
        {
            List<CellDose> doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.SetDose(doses[i]);
            if (changed) RefreshZoneTexture(zone);
        }

        public static void ClearZone(ContaminationZone zone)
        {
            List<CellDose> doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.ClearCell(doses[i].Index);
            if (changed) RefreshZoneTexture(zone);
        }

        /// <summary>ゾーン付近に稼働中の除染施設があるか（ゾーン半径＋施設効果範囲内）。</summary>
        private static bool IsDecontaminated(ContaminationZone zone)
        {
            float reach = zone.Radius + ModConfig.DecontaminationFacilityRange;
            float reach2 = reach * reach;
            for (int i = 0; i < _facilities.Count; i++)
            {
                float dx = _facilities[i].x - zone.CenterX;
                float dz = _facilities[i].z - zone.CenterZ;
                if (dx * dx + dz * dz <= reach2) return true;
            }
            return false;
        }

        /// <summary>BuildingManager を走査し、稼働中の除染施設（名称に Decontamination を含む）の位置を集める。</summary>
        private static void ScanFacilities()
        {
            _facilities.Clear();
            BuildingManager bm = BuildingManager.instance;
            if (bm == null) return;
            Building[] buffer = bm.m_buildings.m_buffer;
            if (buffer == null) return;

            for (int i = 1; i < buffer.Length; i++)
            {
                Building.Flags flags = buffer[i].m_flags;
                if ((flags & Building.Flags.Created) == 0) continue;
                if ((flags & Building.Flags.Completed) == 0) continue;
                const Building.Flags dead = Building.Flags.Abandoned | Building.Flags.BurnedDown
                    | Building.Flags.Collapsed | Building.Flags.Deleted;
                if ((flags & dead) != 0) continue;

                BuildingInfo info = buffer[i].Info;
                string name = info != null ? info.name : null;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.IndexOf(ModConfig.DecontaminationKeyword, StringComparison.OrdinalIgnoreCase) < 0) continue;

                _facilities.Add(buffer[i].m_position);
            }
        }

        private static void RefreshZoneTexture(ContaminationZone zone)
        {
            int cellRadius = (int)(zone.Radius / PollutionGrid.CellSize) + 1;
            int cx = PollutionGrid.WorldToCell(zone.CenterX);
            int cz = PollutionGrid.WorldToCell(zone.CenterZ);
            int minX = Clamp(cx - cellRadius), maxX = Clamp(cx + cellRadius);
            int minZ = Clamp(cz - cellRadius), maxZ = Clamp(cz + cellRadius);
            PollutionField.Refresh(minX, minZ, maxX, maxZ);
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > PollutionGrid.Resolution - 1) return PollutionGrid.Resolution - 1;
            return v;
        }
    }
}
