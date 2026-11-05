namespace IronRing.Save
{
    /// <summary>스펙 003. 빌드 목록에 올라간 씬 이름. 타이틀(스펙 002)과 게임 메뉴가 함께 쓴다.</summary>
    public static class SceneNames
    {
        public const string TITLE = "Title";
        public const string MAIN = "Main";

        /// <summary>타이틀 씬 이름.</summary>
        public static string Title => TITLE;

        /// <summary>플레이 씬 이름.</summary>
        public static string Main => MAIN;
    }
}
