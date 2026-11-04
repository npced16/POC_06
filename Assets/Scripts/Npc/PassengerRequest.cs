using System.Collections.Generic;

using IronRing.Localization;

namespace IronRing.Npc
{
    /// <summary>승객이 원하는 건물과 합류할 인물을 짝지어 표시한다.</summary>
    public static class PassengerRequest
    {
        /// <summary>
        /// npc의 HabitatTypeId와 Name으로 건물 완성 후 합류 안내를 만든다.
        /// 요청 건물이 없으면 null을 반환한다.
        /// </summary>
        public static string Label(NpcDefinition npc)
        {
            if (npc == null || string.IsNullOrEmpty(npc.HabitatTypeId)) return null;
            string habitatName;
            try { habitatName = Strings.Get("habitat_type_" + npc.HabitatTypeId); }
            catch (KeyNotFoundException) { habitatName = npc.HabitatTypeId; }
            return string.Format(Strings.Get("passengers_wants"), habitatName, npc.Name);
        }
    }
}
