namespace Drumix.EditorTools
{
    /// Palabras clave con las que ARMAR TODO busca assets del proyecto (C# puro: los tests las prueban contra
    /// la lista REAL de archivos del equipo).
    public static class DrumixKeywords
    {
        public static readonly string[] Kit = { "drum-kit", "drumkit", "drum kit", "drumset", "drum set", "bateria", "drums" };
        public static readonly string[] KitExclude = { "stick", "sound", "audio", "sfx", "pad" };

        public static readonly string[] Stick = { "palitotambor", "drumstick", "drum stick", "baqueta", "palillo", "palito", "stick" };
        public static readonly string[] StickExclude = { "joystick", "controller", "hand", "lever", "kit" };

        public static readonly string[] Stage = { "escenario", "stage", "venue", "bar interior", "pub interior", "nightclub", "concert" };
        public static readonly string[] StageExclude = { "drum", "bateria", "stick", "character", "npc", "monitor", "light", "speaker", "prop", "button", "ui", "cocina", "kitchen" };

        public static readonly string[] Crowd = { "audience", "publico", "npc", "crowd", "person", "people", "character", "human", "civilian", "citizen" };
        public static readonly string[] CrowdExclude = { "xr", "hand", "hands", "controller", "origin", "camera", "drum", "stick", "rig", "player", "ui", "anim", "animation" };

        public static readonly string[] Tomato = { "tomato", "tomate", "apple", "manzana" };
        public static readonly string[] ThrowExclude = { "crate", "shelf", "machine", "pack" };

        public static readonly string[] UiHover = { "hover", "uihover" };
        public static readonly string[] UiClick = { "click", "uiclick", "button", "clicboton", "pop" };
        public static readonly string[] UiExclude = { "drum", "crowd" };

        public static readonly string[] Cheer = { "cheer", "applause", "aplauso", "ovacion", "clapping" };
        public static readonly string[] Boo = { "booing", "boo", "abucheo" };
        public static readonly string[] Murmur = { "crowd", "murmur", "murmullo", "ambience", "ambiente", "chatter" };
        public static readonly string[] CrowdClipExclude = { "drum", "kick", "snare" };
    }
}
