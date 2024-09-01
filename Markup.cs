using VampireCommandFramework;

namespace GatorBounty;

internal static class Markup
{
	public static string Highlight(int i) => Highlight(i.ToString());

	public static string Highlight(string s) => s.Bold().Color(HighlightColor);
	public static string Secondary(int i) => Secondary(i.ToString());

	public static string Secondary(string s) => s.Bold().Color(SecondaryColor);

	public const string HighlightColor = "#def";

	public const string SecondaryColor = "#dda";

	public static string Prefix = $"[GB] ".Color("#ed1").Bold();

}
