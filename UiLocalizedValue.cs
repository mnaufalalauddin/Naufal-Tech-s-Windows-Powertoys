using System;

namespace Naufal_Windows_Tech_s_Powertoys;

// Keep canonical text separate from its last rendering. Never reverse-translate
// from translated UI text, and never use display text as a backend identifier.
internal sealed class UiLocalizedValue
{
    internal string? Source { get; private set; }
    internal string? Rendered { get; private set; }

    internal string Resolve(string current, string language, Func<string, string, string> translate)
    {
        if (Source is null || !string.Equals(current, Rendered, StringComparison.Ordinal)) Source = current;
        Rendered = translate(Source, language);
        return Rendered;
    }

    internal string RestoreSource(string current)
    {
        // A last backend update must not be overwritten during detach/close.
        return Source is not null && string.Equals(current, Rendered, StringComparison.Ordinal)
            ? Source : current;
    }
}
