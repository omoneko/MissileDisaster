using ColossalFramework.UI;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// 弾頭種別・核出力(kt)・爆発高度を選ぶ常設パネル（UIView 直下）。選択は MissileTool の静的値へ反映し、
    /// 「照準開始」で MissileTool を起動→マップクリックで発射する。核出力はカタログ(10種)選択と kt 手入力の両対応。
    /// AlienInvasion.InvasionUI と同じく、レベルロードで Create、アンロードで Destroy して静的状態を残さない。
    /// UIComponent はメインスレッドで生成すること。
    /// </summary>
    public static class MissilePanel
    {
        private const string PanelName = "MissileDisasterControlPanel";
        private const string SpriteNormal = "ButtonMenu";
        private const string SpriteSelected = "ButtonMenuFocused";

        private static readonly WarheadType[] Warheads =
        {
            WarheadType.Conventional, WarheadType.Cluster, WarheadType.WhitePhosphorus,
            WarheadType.Thermobaric, WarheadType.Nuclear,
        };
        private static readonly string[] WarheadLabels =
            { "Conventional", "Cluster", "White Phosphorus", "Thermobaric", "Nuclear" };

        private static readonly BurstType[] Bursts = { BurstType.Airburst, BurstType.Groundburst };
        private static readonly string[] BurstLabels = { "Air Burst", "Ground Burst" };

        private static UIPanel _panel;
        private static UIButton[] _warheadButtons;
        private static UIButton[] _burstButtons;
        private static UITextField _ktField;
        private static UITextField _kgField;

        /// <summary>レベルロード時にメインスレッドから呼ぶ。パネルを生成する。</summary>
        public static void Create()
        {
            try
            {
                UIView view = UIView.GetAView();
                if (view == null) { ModConfig.LogError("MissilePanel.Create: UIView が null"); return; }
                if (view.FindUIComponent<UIPanel>(PanelName) != null) return; // 二重生成防止

                _panel = view.AddUIComponent(typeof(UIPanel)) as UIPanel;
                if (_panel == null) { ModConfig.LogError("MissilePanel.Create: UIPanel 生成失敗"); return; }
                _panel.name = PanelName;
                _panel.backgroundSprite = "MenuPanel2";
                _panel.width = ModConfig.PanelWidth;
                _panel.relativePosition = new Vector3(ModConfig.PanelPosX, ModConfig.PanelPosY);

                float y = BuildContents();
                _panel.height = y + 8f;

                RefreshHighlight();
                _panel.Hide(); // 既定は非表示。災害タブのミサイルボタンで開く。
                ModConfig.Log("MissilePanel を生成しました");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissilePanel.Create error: " + e);
            }
        }

        /// <summary>毎フレーム呼んでよい。パネルが未生成なら生成する（UIView 準備前の失敗をリトライして確実に出す）。</summary>
        public static void EnsureCreated()
        {
            if (_panel == null) Create();
        }

        /// <summary>パネルを画面内の既定位置へ戻して表示する（画面外に消えた・見つからない時の復帰用）。</summary>
        public static void ResetPosition()
        {
            EnsureCreated();
            if (_panel != null)
            {
                _panel.relativePosition = new Vector3(ModConfig.PanelPosX, ModConfig.PanelPosY);
                Show();
            }
        }

        /// <summary>災害タブのミサイルボタンから呼ぶ。表示中なら隠し、非表示なら出す。</summary>
        public static void Toggle()
        {
            EnsureCreated();
            if (_panel == null) return;
            if (_panel.isVisible) Hide(); else Show();
        }

        /// <summary>パネルを表示して前面へ。</summary>
        public static void Show()
        {
            EnsureCreated();
            if (_panel != null) { _panel.Show(); _panel.BringToFront(); }
        }

        /// <summary>災害タブのミサイルアイコンから呼ぶ：パネルを出し、そのまま照準ツールを起動する。</summary>
        public static void ShowAndStartTargeting()
        {
            Show();
            StartAiming(); // ToolsModifierControl.SetTool<MissileTool>()
        }

        /// <summary>パネルを隠す（災害タブのボタンから開き直せる）。</summary>
        public static void Hide()
        {
            if (_panel != null) _panel.Hide();
        }

        /// <summary>レベルアンロード時に呼ぶ。パネルを破棄し参照を捨てる（静的状態を残さない）。</summary>
        public static void Destroy()
        {
            try
            {
                if (_panel != null)
                {
                    Object.Destroy(_panel.gameObject);
                    _panel = null;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissilePanel.Destroy error: " + e);
            }
            finally
            {
                _warheadButtons = null;
                _burstButtons = null;
                _ktField = null;
                _kgField = null;
            }
        }

        private static float BuildContents()
        {
            float pad = 8f;
            float w = ModConfig.PanelWidth - pad * 2f;
            float y = pad;

            // タイトル（ドラッグ可能ハンドル）
            UILabel title = _panel.AddUIComponent<UILabel>();
            title.text = "Missile Launch Control";
            title.textScale = 0.9f;
            title.relativePosition = new Vector3(pad, y);
            var drag = _panel.AddUIComponent<UIDragHandle>();
            drag.target = _panel;
            drag.width = ModConfig.PanelWidth;
            drag.height = 24f;
            drag.relativePosition = new Vector3(0f, 0f);

            // 閉じるボタン（右上）。ドラッグハンドルより後に追加＝手前でクリックを受ける。
            // 閉じても災害タブのミサイルボタンから開き直せる。
            UIButton closeBtn = MakeButton("✕", ModConfig.PanelWidth - 26f, 3f, 22f);
            closeBtn.textScale = 0.9f;
            closeBtn.tooltip = "Close (reopen from the Missile button in the Disasters panel)";
            closeBtn.eventClick += (c, p) => Hide();
            y += 26f;

            // 弾頭選択
            y = AddSectionLabel("Warhead", pad, y);
            _warheadButtons = new UIButton[Warheads.Length];
            for (int i = 0; i < Warheads.Length; i++)
            {
                WarheadType type = Warheads[i]; // クロージャ用にローカルへ束縛
                UIButton b = MakeButton(WarheadLabels[i], pad, y, w);
                b.eventClick += (c, p) => { MissileTool.CurrentWarhead = type; RefreshHighlight(); };
                _warheadButtons[i] = b;
                y += ModConfig.PanelButtonHeight + ModConfig.PanelButtonGap;
            }

            // 核出力（カタログ選択 or kt 手入力）
            y += 4f;
            y = AddSectionLabel("Nuclear Yield (nuclear only, kt)", pad, y);

            UIDropDown dd = MakeWeaponDropdown(pad, y, w);
            y += ModConfig.PanelButtonHeight + ModConfig.PanelButtonGap;

            _ktField = MakeKtField(pad, y, w);
            y += 26f + ModConfig.PanelButtonGap;

            // 通常爆弾の出力（kg TNT 手入力・非核弾頭に適用）
            y += 4f;
            y = AddSectionLabel("Conventional Yield (non-nuclear, kg TNT)", pad, y);
            _kgField = MakeKgField(pad, y, w);
            y += 26f + ModConfig.PanelButtonGap;

            // 爆発高度（空中/地上）
            y += 4f;
            y = AddSectionLabel("Burst Height", pad, y);
            _burstButtons = new UIButton[Bursts.Length];
            float halfW = (w - ModConfig.PanelButtonGap) * 0.5f;
            for (int i = 0; i < Bursts.Length; i++)
            {
                BurstType bt = Bursts[i];
                float bx = pad + i * (halfW + ModConfig.PanelButtonGap);
                UIButton b = MakeButton(BurstLabels[i], bx, y, halfW);
                b.eventClick += (c, p) => { MissileTool.CurrentBurst = bt; RefreshHighlight(); };
                _burstButtons[i] = b;
            }
            y += ModConfig.PanelButtonHeight + ModConfig.PanelButtonGap;

            // 照準開始（ツール起動）
            y += 6f;
            UIButton launch = MakeButton("Start Targeting (click to launch)", pad, y, w);
            launch.color = new Color32(255, 190, 120, 255);
            launch.eventClick += (c, p) => StartAiming();
            y += ModConfig.PanelButtonHeight + ModConfig.PanelButtonGap;

            return y;
        }

        private static UIDropDown MakeWeaponDropdown(float x, float y, float width)
        {
            UIDropDown dd = _panel.AddUIComponent<UIDropDown>();
            dd.size = new Vector2(width, ModConfig.PanelButtonHeight);
            dd.relativePosition = new Vector3(x, y);
            dd.normalBgSprite = "ButtonMenu";
            dd.hoveredBgSprite = "ButtonMenuHovered";
            dd.disabledBgSprite = "ButtonMenuDisabled";
            dd.listBackground = "GenericPanelLight";
            dd.itemHeight = 22;
            dd.itemHover = "ListItemHover";
            dd.itemHighlight = "ListItemHighlight";
            dd.listWidth = (int)width;
            dd.listHeight = 220;
            dd.listPosition = UIDropDown.PopupListPosition.Below;
            dd.textScale = 0.75f;
            dd.textFieldPadding = new RectOffset(8, 8, 6, 0);
            dd.itemPadding = new RectOffset(8, 0, 3, 0);
            dd.popupColor = new Color32(45, 52, 61, 255);
            dd.popupTextColor = new Color32(230, 230, 230, 255);
            dd.foregroundSpriteMode = UIForegroundSpriteMode.Stretch;
            dd.verticalAlignment = UIVerticalAlignment.Middle;
            dd.horizontalAlignment = UIHorizontalAlignment.Left;

            NuclearWeapon[] catalog = NuclearWeapons.Catalog;
            var items = new string[catalog.Length];
            for (int i = 0; i < catalog.Length; i++)
            {
                items[i] = catalog[i].Name + " (" + catalog[i].Kilotons + "kt)";
            }
            dd.items = items;
            dd.selectedIndex = -1; // 既定は未選択（kt は手入力の既定値を使う）

            // トリガーボタン（右端の▼）
            UIButton trigger = dd.AddUIComponent<UIButton>();
            trigger.text = "▼";
            trigger.textScale = 0.7f;
            trigger.size = new Vector2(24f, ModConfig.PanelButtonHeight);
            trigger.relativePosition = new Vector3(width - 24f, 0f);
            trigger.normalBgSprite = "ButtonMenu";
            trigger.hoveredBgSprite = "ButtonMenuHovered";
            trigger.pressedBgSprite = "ButtonMenuPressed";
            dd.triggerButton = trigger;

            dd.eventSelectedIndexChanged += (c, index) =>
            {
                if (index < 0 || index >= catalog.Length) return;
                MissileTool.CurrentYieldKilotons = catalog[index].Kilotons;
                if (_ktField != null) _ktField.text = catalog[index].Kilotons.ToString();
            };
            return dd;
        }

        private static UITextField MakeNumericField(float x, float y, float width, int initial, string tooltip)
        {
            UITextField tf = _panel.AddUIComponent<UITextField>();
            tf.size = new Vector2(width, 26f);
            tf.relativePosition = new Vector3(x, y);
            tf.builtinKeyNavigation = true;
            tf.readOnly = false;
            tf.numericalOnly = true;
            tf.allowFloats = false;
            tf.maxLength = 8;
            tf.selectionSprite = "EmptySprite";
            tf.normalBgSprite = "TextFieldPanel";
            tf.hoveredBgSprite = "TextFieldPanelHovered";
            tf.focusedBgSprite = "TextFieldPanel";
            tf.textScale = 0.85f;
            tf.textColor = Color.white;
            tf.padding = new RectOffset(8, 8, 6, 6);
            tf.horizontalAlignment = UIHorizontalAlignment.Left;
            tf.verticalAlignment = UIVerticalAlignment.Middle;
            tf.text = initial.ToString();
            tf.tooltip = tooltip;
            return tf;
        }

        private static UITextField MakeKtField(float x, float y, float width)
        {
            UITextField tf = MakeNumericField(x, y, width, MissileTool.CurrentYieldKilotons, "Enter yield in kt (press Enter)");
            tf.eventTextSubmitted += (c, s) => ApplyKtText(s);
            tf.eventLostFocus += (c, p) => { if (_ktField != null) ApplyKtText(_ktField.text); };
            return tf;
        }

        private static UITextField MakeKgField(float x, float y, float width)
        {
            UITextField tf = MakeNumericField(x, y, width, MissileTool.CurrentConventionalKilograms, "Enter charge in kg TNT (press Enter)");
            tf.eventTextSubmitted += (c, s) => ApplyKgText(s);
            tf.eventLostFocus += (c, p) => { if (_kgField != null) ApplyKgText(_kgField.text); };
            return tf;
        }

        private static void ApplyKtText(string s)
        {
            int kt;
            if (int.TryParse(s, out kt) && kt > 0)
            {
                MissileTool.CurrentYieldKilotons = kt;
            }
            else if (_ktField != null)
            {
                _ktField.text = MissileTool.CurrentYieldKilotons.ToString(); // 不正入力は現在値へ戻す
            }
        }

        private static void ApplyKgText(string s)
        {
            int kg;
            if (int.TryParse(s, out kg) && kg > 0)
            {
                MissileTool.CurrentConventionalKilograms = kg;
            }
            else if (_kgField != null)
            {
                _kgField.text = MissileTool.CurrentConventionalKilograms.ToString(); // 不正入力は現在値へ戻す
            }
        }

        private static float AddSectionLabel(string text, float x, float y)
        {
            UILabel label = _panel.AddUIComponent<UILabel>();
            label.text = text;
            label.textScale = 0.75f;
            label.textColor = new Color32(200, 200, 200, 255);
            label.relativePosition = new Vector3(x, y);
            return y + 18f;
        }

        private static UIButton MakeButton(string text, float x, float y, float width)
        {
            UIButton b = _panel.AddUIComponent<UIButton>();
            b.text = text;
            b.textScale = 0.8f;
            b.size = new Vector2(width, ModConfig.PanelButtonHeight);
            b.relativePosition = new Vector3(x, y);
            b.normalBgSprite = SpriteNormal;
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            return b;
        }

        /// <summary>選択中の弾頭・爆発高度ボタンをハイライトする。</summary>
        private static void RefreshHighlight()
        {
            if (_warheadButtons != null)
            {
                for (int i = 0; i < _warheadButtons.Length; i++)
                {
                    if (_warheadButtons[i] == null) continue;
                    _warheadButtons[i].normalBgSprite =
                        Warheads[i] == MissileTool.CurrentWarhead ? SpriteSelected : SpriteNormal;
                }
            }
            if (_burstButtons != null)
            {
                for (int i = 0; i < _burstButtons.Length; i++)
                {
                    if (_burstButtons[i] == null) continue;
                    _burstButtons[i].normalBgSprite =
                        Bursts[i] == MissileTool.CurrentBurst ? SpriteSelected : SpriteNormal;
                }
            }
        }

        private static void StartAiming()
        {
            try
            {
                ToolsModifierControl.SetTool<MissileTool>();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissilePanel.StartAiming error: " + e);
            }
        }
    }
}
