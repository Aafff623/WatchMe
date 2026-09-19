namespace WatchMe.Core;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>Parses hotkey strings like "Ctrl+Alt+W" into Win32 modifier flags and a virtual key.</summary>
public static class HotkeyPattern
{
    public static bool TryParse(string? display, out HotkeyModifiers modifiers, out uint virtualKey)
    {
        modifiers = HotkeyModifiers.None;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(display))
            return false;

        var parts = display.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sawKey = false;
        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "ALT":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "SHIFT":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "WIN" or "META" or "SUPER":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                default:
                    if (sawKey || !TryParseKey(part, out virtualKey))
                        return false;
                    sawKey = true;
                    break;
            }
        }

        return sawKey;
    }

    private static bool TryParseKey(string token, out uint vk)
    {
        token = token.ToUpperInvariant();
        vk = 0;
        if (token.Length == 1)
        {
            var c = token[0];
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                vk = c;
                return true;
            }

            return false;
        }

        vk = token switch
        {
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "SPACE" => 0x20, "TAB" => 0x09, "ENTER" => 0x0D,
            _ => 0,
        };
        return vk != 0;
    }
}
