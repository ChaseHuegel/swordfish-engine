namespace Swordfish.Library.IO
{
    public enum Key
    {
        NONE = -1,
        BACKSPACE = 8,
        TAB = 9,
        ENTER = 13,
        SHIFT = 16,
        CONTROL = 17,
        ALT = 18,
        PAUSE = 19,
        CAPSLOCK = 20,
        ESC = 27,
        SPACE = 32,
        PAGE_UP = 33,
        PAGE_DOWN = 34,
        END = 35,
        HOME = 36,
        LEFT_ARROW = 37,
        UP_ARROW = 38,
        RIGHT_ARROW = 39,
        DOWN_ARROW = 40,
        SELECT = 41,
        PRINT = 42,
        EXECUTE = 43,
        PRINT_SCREEN = 44,
        INSERT = 45,
        DELETE = 46,
        HELP = 47,
        D0 = 48,
        D1 = 49,
        D2 = 50,
        D3 = 51,
        D4 = 52,
        D5 = 53,
        D6 = 54,
        D7 = 55,
        D8 = 56,
        D9 = 57,
        A = 65,
        B = 66,
        C = 67,
        D = 68,
        E = 69,
        F = 70,
        G = 71,
        H = 72,
        I = 73,
        J = 74,
        K = 75,
        L = 76,
        M = 77,
        N = 78,
        O = 79,
        P = 80,
        Q = 81,
        R = 82,
        S = 83,
        T = 84,
        U = 85,
        V = 86,
        W = 87,
        X = 88,
        Y = 89,
        Z = 90,
        LEFT_WINDOWS = 91,
        RIGHT_WINDOWS = 92,
        APPLICATION = 93,
        SLEEP = 95,
        NUMPAD_0 = 96,
        NUMPAD_1 = 97,
        NUMPAD_2 = 98,
        NUMPAD_3 = 99,
        NUMPAD_4 = 100,
        NUMPAD_5 = 101,
        NUMPAD_6 = 102,
        NUMPAD_7 = 103,
        NUMPAD_8 = 104,
        NUMPAD_9 = 105,
        MULTIPLY = 106,
        ADD = 107,
        SEPARATOR = 108,
        SUBTRACT = 109,
        DECIMAL = 110,
        DIVIDE = 111,
        F1 = 112,
        F2 = 113,
        F3 = 114,
        F4 = 115,
        F5 = 116,
        F6 = 117,
        F7 = 118,
        F8 = 119,
        F9 = 120,
        F10 = 121,
        F11 = 122,
        F12 = 123,
        F13 = 124,
        F14 = 125,
        F15 = 126,
        F16 = 127,
        F17 = 128,
        F18 = 120,
        F19 = 130,
        F20 = 131,
        F21 = 132,
        F22 = 133,
        F23 = 134,
        F24 = 135,
        NUMLOCK = 144,
        SCROLL_LOCK = 145,
        LEFT_SHIFT = 160,
        RIGHT_SHIFT = 161,
        LEFT_CONTROL = 162,
        RIGHT_CONTROL = 163,
        LEFT_ALT = 164,
        RIGHT_ALT = 165,
    }

    public static class KeyExtensions
    {
        public static string ToDisplayString(this Key key)
        {
            if (key >= Key.D0 && key <= Key.D9)
                return (key - Key.D0).ToString();

            return key.ToString().Replace('_', ' ');
        }
    }
}
