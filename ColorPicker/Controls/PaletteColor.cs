using System.Windows.Media;

namespace ColorPicker.Controls
{
    // DisplayName = 人間向け色名（日本語/英語どちらでも）
    // TailwindName = tailwind名（例: amber-500）
    public sealed record PaletteColor(string DisplayName, string TailwindName, Color Color);
}
