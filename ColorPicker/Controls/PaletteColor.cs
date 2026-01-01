using System.Windows.Media;

namespace ColorPicker.Controls
{
    // DisplayName = 人間向け色名（日本語/英語どちらでも）
    // TailwindName = tailwind名（例: amber-500）
    public sealed class PaletteColor
    {
        public string DisplayName { get; }
        public string TailwindName { get; }
        public Color Color { get; }

        public PaletteColor(string displayName, string tailwindName, Color color)
        {
            DisplayName = displayName;
            TailwindName = tailwindName;
            Color = color;
        }
    }
}
