using System;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("RightToLeft", "Wulf/lukespragg", "0.2.1", ResourceId = 2313)]
    [Description("Reverses chat text to for RTL (right to left) support")]

    class RightToLeft : CovalencePlugin
    {
        #region Chat Formatting

        object OnUserChat(IPlayer player, string message)
        {
            if (!IsRightToLeft(message)) return null;

            var color = player.IsAdmin ? "#aaff55" : "#55aaff";
            message = covalence.FormatText($"[{color}]{player.Name}[/#]") + ": " + RtlText(message);
#if RUST
            ConsoleNetwork.BroadcastToAllClients("chat.add", player.Id, message);
#else
            server.Broadcast(message);
#endif
            return true;
        }

        #endregion

        #region Helpers

        string RtlText(string text)
        {
            var characters = text.ToCharArray();
            Array.Reverse(characters);
            string reversed = null;
            for (var i = 0; i <= characters.Length - 1; i++) reversed += characters.GetValue(i);
            return IsArabic(text) ? reversed.Replace(" ", "") : reversed;
        }

        static bool IsArabic(string text)
        {
            var glyphs = text.ToCharArray();
            foreach (var glyph in glyphs)
            {
                if (glyph >= 0x600 && glyph <= 0x6ff) return true;
                if (glyph >= 0x750 && glyph <= 0x77f) return true;
                if (glyph >= 0xfb50 && glyph <= 0xfc3f) return true;
                if (glyph >= 0xfe70 && glyph <= 0xfefc) return true;
            }
            return false;
        }

        #region RTL Check

        bool IsRightToLeft(string text)
        {
            foreach (var c in text)
            {
                if (c >= 0x5BE && c <= 0x10B7F)
                {
                    if (c <= 0x85E)
                    {
                        if (c == 0x5BE) return true;
                        else if (c == 0x5C0) return true;
                        else if (c == 0x5C3) return true;
                        else if (c == 0x5C6) return true;
                        else if (0x5D0 <= c && c <= 0x5EA) return true;
                        else if (0x5F0 <= c && c <= 0x5F4) return true;
                        else if (c == 0x608) return true;
                        else if (c == 0x60B) return true;
                        else if (c == 0x60D) return true;
                        else if (c == 0x61B) return true;
                        else if (0x61E <= c && c <= 0x64A) return true;
                        else if (0x66D <= c && c <= 0x66F) return true;
                        else if (0x671 <= c && c <= 0x6D5) return true;
                        else if (0x6E5 <= c && c <= 0x6E6) return true;
                        else if (0x6EE <= c && c <= 0x6EF) return true;
                        else if (0x6FA <= c && c <= 0x70D) return true;
                        else if (c == 0x710) return true;
                        else if (0x712 <= c && c <= 0x72F) return true;
                        else if (0x74D <= c && c <= 0x7A5) return true;
                        else if (c == 0x7B1) return true;
                        else if (0x7C0 <= c && c <= 0x7EA) return true;
                        else if (0x7F4 <= c && c <= 0x7F5) return true;
                        else if (c == 0x7FA) return true;
                        else if (0x800 <= c && c <= 0x815) return true;
                        else if (c == 0x81A) return true;
                        else if (c == 0x824) return true;
                        else if (c == 0x828) return true;
                        else if (0x830 <= c && c <= 0x83E) return true;
                        else if (0x840 <= c && c <= 0x858) return true;
                        else if (c == 0x85E) return true;
                    }
                    else if (c == 0x200F) return true;
                    else if (c >= 0xFB1D)
                    {
                        if (c == 0xFB1D) return true;
                        else if (0xFB1F <= c && c <= 0xFB28) return true;
                        else if (0xFB2A <= c && c <= 0xFB36) return true;
                        else if (0xFB38 <= c && c <= 0xFB3C) return true;
                        else if (c == 0xFB3E) return true;
                        else if (0xFB40 <= c && c <= 0xFB41) return true;
                        else if (0xFB43 <= c && c <= 0xFB44) return true;
                        else if (0xFB46 <= c && c <= 0xFBC1) return true;
                        else if (0xFBD3 <= c && c <= 0xFD3D) return true;
                        else if (0xFD50 <= c && c <= 0xFD8F) return true;
                        else if (0xFD92 <= c && c <= 0xFDC7) return true;
                        else if (0xFDF0 <= c && c <= 0xFDFC) return true;
                        else if (0xFE70 <= c && c <= 0xFE74) return true;
                        else if (0xFE76 <= c && c <= 0xFEFC) return true;
                        else if (0x10800 <= c && c <= 0x10805) return true;
                        else if (c == 0x10808) return true;
                        else if (0x1080A <= c && c <= 0x10835) return true;
                        else if (0x10837 <= c && c <= 0x10838) return true;
                        else if (c == 0x1083C) return true;
                        else if (0x1083F <= c && c <= 0x10855) return true;
                        else if (0x10857 <= c && c <= 0x1085F) return true;
                        else if (0x10900 <= c && c <= 0x1091B) return true;
                        else if (0x10920 <= c && c <= 0x10939) return true;
                        else if (c == 0x1093F) return true;
                        else if (c == 0x10A00) return true;
                        else if (0x10A10 <= c && c <= 0x10A13) return true;
                        else if (0x10A15 <= c && c <= 0x10A17) return true;
                        else if (0x10A19 <= c && c <= 0x10A33) return true;
                        else if (0x10A40 <= c && c <= 0x10A47) return true;
                        else if (0x10A50 <= c && c <= 0x10A58) return true;
                        else if (0x10A60 <= c && c <= 0x10A7F) return true;
                        else if (0x10B00 <= c && c <= 0x10B35) return true;
                        else if (0x10B40 <= c && c <= 0x10B55) return true;
                        else if (0x10B58 <= c && c <= 0x10B72) return true;
                        else if (0x10B78 <= c && c <= 0x10B7F) return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #endregion
    }
}