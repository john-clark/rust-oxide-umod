using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Note Write Control", "Gachl", "1.0.1")]
    [Description("Control how players can write notes (create new, append to, or edit all)")]
    class NoteWriteControl : RustPlugin
    {
        private static readonly string PERMISSION_WRITE = "notewritecontrol.canwrite";
        private static readonly string PERMISSION_APPEND = "notewritecontrol.canappend";
        private static readonly string PERMISSION_EDIT = "notewritecontrol.canedit";

        private void Init()
        {
            permission.RegisterPermission(NoteWriteControl.PERMISSION_APPEND, this);
            permission.RegisterPermission(NoteWriteControl.PERMISSION_WRITE, this);
            permission.RegisterPermission(NoteWriteControl.PERMISSION_EDIT, this);
        }

        [ConsoleCommand("note.update")]
        void NoteUpdateCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Args.Length != 2)
                return;

            uint noteUID = 0;
            if (!uint.TryParse(arg.Args[0], out noteUID))
                return;

            BasePlayer editor = arg.Player();

            if (editor == null)
                return;

            Item note = editor.inventory.FindItemUID(noteUID);
            if (note == null)
                return;

            string currentText = note.text ?? "";
            string newText = arg.Args[1];

            bool editPermOrAdmin = editor.IPlayer.HasPermission(NoteWriteControl.PERMISSION_EDIT) || editor.IsAdmin || editor.IsDeveloper;
            bool writePermAndEmpty = editor.IPlayer.HasPermission(NoteWriteControl.PERMISSION_WRITE) && String.IsNullOrEmpty(currentText);
            bool appendPermAndHasAppended = editor.IPlayer.HasPermission(NoteWriteControl.PERMISSION_APPEND) && currentText.Length > 0 && newText.Length > currentText.Length && newText.Substring(0, currentText.Length) == currentText;

            if (editPermOrAdmin || writePermAndEmpty || appendPermAndHasAppended)
                note.text = newText;

            note.MarkDirty();
        }
    }
}
