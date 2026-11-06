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
            string habitatName = HabitatName(npc);
            return habitatName != null ? string.Format(Strings.Get("passengers_wants"), habitatName, npc.Name) : null;
        }

        /// <summary>
        /// 승객 목록 줄에 쓸 이름을 만든다. displayName 뒤 괄호에 npc의 서식지 이름을 붙여 반환한다.
        /// 서식지가 없으면 displayName을 그대로 반환한다.
        /// </summary>
        public static string RowName(NpcDefinition npc, string displayName)
        {
            string habitatName = HabitatName(npc);
            return habitatName != null ? string.Format(Strings.Get("passengers_row_name"), displayName, habitatName) : displayName;
        }

        /// <summary>npc의 HabitatTypeId로 서식지 표시 이름을 반환한다. 서식지가 없으면 null, 문구가 없으면 id를 반환한다.</summary>
        private static string HabitatName(NpcDefinition npc)
        {
            if (npc == null || string.IsNullOrEmpty(npc.HabitatTypeId)) return null;
            try { return Strings.Get("habitat_type_" + npc.HabitatTypeId); }
            catch (KeyNotFoundException) { return npc.HabitatTypeId; }
        }

        /// <summary>
        /// npc의 건설 요청(Label)에 직무 효과(WorkKind, WorkBonus)를 붙인 안내를 만든다.
        /// 요청이 없으면 직무 효과만, 둘 다 없으면 빈 문자열을 반환한다.
        /// </summary>
        public static string LabelWithWork(NpcDefinition npc)
        {
            string label = Label(npc) ?? string.Empty;
            if (npc == null || npc.WorkBonus <= 0) return label;
            string work = WorkLabel(npc.WorkKind) + " +" + npc.WorkBonus;
            return label.Length > 0 ? label + " · " + work : work;
        }

        /// <summary>직무 종류 kind를 받아 알림과 사건 창에 쓸 짧은 한국어 효과 이름을 반환한다.</summary>
        public static string WorkLabel(string kind)
        {
            switch (kind)
            {
                case "income": return "매 바퀴 마르크";
                case "housing": return "주거";
                case "light": return "빛";
                default: return "일손";
            }
        }

        /// <summary>name 뒤에 주격 조사(이/가)를 붙여 반환한다. 마지막 글자에 받침이 있으면 "이", 없거나 한글이 아니면 "가".</summary>
        public static string Subject(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            char last = name[name.Length - 1];
            bool hangul = last >= '가' && last <= '힣';
            bool batchim = hangul && (last - 0xAC00) % 28 != 0;
            return name + (batchim ? "이" : "가");
        }
    }
}
