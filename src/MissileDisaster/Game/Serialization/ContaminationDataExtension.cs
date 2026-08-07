using System.Collections.Generic;
using ICities;
using MissileDisaster.Core;

namespace MissileDisaster.Game.Serialization
{
    /// <summary>Persists the radioactive contamination zone ledger into the save game. The game finds and drives this itself.</summary>
    public class ContaminationDataExtension : SerializableDataExtensionBase
    {
        private const string DataId = "MissileDisaster.Contamination.v1";

        public override void OnSaveData()
        {
            try
            {
                List<ContaminationZone> zones = Contamination.ContaminationManager.Zones;
                byte[] bytes = ZoneSerializer.Serialize(zones);
                serializableDataManager.SaveData(DataId, bytes);
                ModConfig.Log("Contamination saved " + zones.Count + " zone(s)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("ContaminationDataExtension.OnSaveData error: " + e);
            }
        }

        public override void OnLoadData()
        {
            try
            {
                byte[] bytes = serializableDataManager.LoadData(DataId);
                List<ContaminationZone> zones = ZoneSerializer.Deserialize(bytes);
                Contamination.ContaminationManager.ReplaceAll(zones);
                ModConfig.Log("Contamination loaded " + zones.Count + " zone(s)");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("ContaminationDataExtension.OnLoadData error: " + e);
            }
        }
    }
}
