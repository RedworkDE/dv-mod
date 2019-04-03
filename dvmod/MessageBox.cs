using System;
using System.Runtime.InteropServices;

namespace dvmod
{
    public class MessageBox
    {

        public static DialogResult Show(object text, string caption = null, MessageBoxButtons buttons = 0, MessageBoxIcon icon = 0, MessageBoxDefaultButton defaultButton = 0, MessageBoxOptions options = 0)
        {
            return (DialogResult)NativeMessageBox(IntPtr.Zero, text?.ToString() ?? "<null>", caption, (int)buttons | (int)icon | (int)defaultButton | (int)options);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
        private static extern int NativeMessageBox(IntPtr hWnd, string text, string caption, int type);
    }

    /// <summary>Specifies identifiers to indicate the return value of a dialog box.</summary>
    public enum DialogResult
    {
        /// <summary>
        /// <see langword="Nothing" /> is returned from the dialog box. This means that the modal dialog continues running.</summary>
        None,
        /// <summary>The dialog box return value is <see langword="OK" /> (usually sent from a button labeled OK).</summary>
        OK,
        /// <summary>The dialog box return value is <see langword="Cancel" /> (usually sent from a button labeled Cancel).</summary>
        Cancel,
        /// <summary>The dialog box return value is <see langword="Abort" /> (usually sent from a button labeled Abort).</summary>
        Abort,
        /// <summary>The dialog box return value is <see langword="Retry" /> (usually sent from a button labeled Retry).</summary>
        Retry,
        /// <summary>The dialog box return value is <see langword="Ignore" /> (usually sent from a button labeled Ignore).</summary>
        Ignore,
        /// <summary>The dialog box return value is <see langword="Yes" /> (usually sent from a button labeled Yes).</summary>
        Yes,
        /// <summary>The dialog box return value is <see langword="No" /> (usually sent from a button labeled No).</summary>
        No,
    }

    /// <summary>Specifies constants defining which buttons to display on a <see cref="T:System.Windows.Forms.MessageBox" />.</summary>
    public enum MessageBoxButtons
    {
        /// <summary>The message box contains an OK button.</summary>
        OK,
        /// <summary>The message box contains OK and Cancel buttons.</summary>
        OKCancel,
        /// <summary>The message box contains Abort, Retry, and Ignore buttons.</summary>
        AbortRetryIgnore,
        /// <summary>The message box contains Yes, No, and Cancel buttons.</summary>
        YesNoCancel,
        /// <summary>The message box contains Yes and No buttons.</summary>
        YesNo,
        /// <summary>The message box contains Retry and Cancel buttons.</summary>
        RetryCancel,
    }

    /// <summary>Specifies constants defining which information to display.</summary>
    public enum MessageBoxIcon
    {
        /// <summary>The message box contain no symbols.</summary>
        None = 0,
        /// <summary>The message box contains a symbol consisting of white X in a circle with a red background.</summary>
        Error = 16, // 0x00000010
        /// <summary>The message box contains a symbol consisting of a white X in a circle with a red background.</summary>
        Hand = 16, // 0x00000010
        /// <summary>The message box contains a symbol consisting of white X in a circle with a red background.</summary>
        Stop = 16, // 0x00000010
        /// <summary>The message box contains a symbol consisting of a question mark in a circle. The question-mark message icon is no longer recommended because it does not clearly represent a specific type of message and because the phrasing of a message as a question could apply to any message type. In addition, users can confuse the message symbol question mark with Help information. Therefore, do not use this question mark message symbol in your message boxes. The system continues to support its inclusion only for backward compatibility.</summary>
        Question = 32, // 0x00000020
        /// <summary>The message box contains a symbol consisting of an exclamation point in a triangle with a yellow background.</summary>
        Exclamation = 48, // 0x00000030
        /// <summary>The message box contains a symbol consisting of an exclamation point in a triangle with a yellow background.</summary>
        Warning = 48, // 0x00000030
        /// <summary>The message box contains a symbol consisting of a lowercase letter i in a circle.</summary>
        Asterisk = 64, // 0x00000040
        /// <summary>The message box contains a symbol consisting of a lowercase letter i in a circle.</summary>
        Information = 64, // 0x00000040
    }

    /// <summary>Specifies constants defining the default button on a <see cref="T:System.Windows.Forms.MessageBox" />.</summary>
    public enum MessageBoxDefaultButton
    {
        /// <summary>The first button on the message box is the default button.</summary>
        Button1 = 0,
        /// <summary>The second button on the message box is the default button.</summary>
        Button2 = 256, // 0x00000100
        /// <summary>The third button on the message box is the default button.</summary>
        Button3 = 512, // 0x00000200
    }

    /// <summary>Specifies options on a <see cref="T:System.Windows.Forms.MessageBox" />.</summary>
    [Flags]
    public enum MessageBoxOptions
    {
        /// <summary>The message box is displayed on the active desktop.</summary>
        ServiceNotification = 2097152, // 0x00200000
        /// <summary>The message box is displayed on the active desktop.</summary>
        DefaultDesktopOnly = 131072, // 0x00020000
        /// <summary>The message box text is right-aligned.</summary>
        RightAlign = 524288, // 0x00080000
        /// <summary>Specifies that the message box text is displayed with right to left reading order.</summary>
        RtlReading = 1048576, // 0x00100000
    }
}