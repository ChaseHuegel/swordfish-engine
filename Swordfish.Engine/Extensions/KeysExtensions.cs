using OpenTK.Windowing.GraphicsLibraryFramework;
using Swordfish.Engine;
using Swordfish.Engine.Rendering;

namespace Swordfish.Engine.Extensions
{
    public static class KeysExtensions
    {
        public static Image2D ICO_A = Image2D.LoadFromFile($"{Directories.ICONS}/controls/a.png", "ico_a");
        public static Image2D ICO_B = Image2D.LoadFromFile($"{Directories.ICONS}/controls/b.png", "ico_b");
        public static Image2D ICO_C = Image2D.LoadFromFile($"{Directories.ICONS}/controls/c.png", "ico_c");
        public static Image2D ICO_D = Image2D.LoadFromFile($"{Directories.ICONS}/controls/d.png", "ico_d");
        public static Image2D ICO_E = Image2D.LoadFromFile($"{Directories.ICONS}/controls/e.png", "ico_e");
        public static Image2D ICO_F = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f.png", "ico_f");
        public static Image2D ICO_G = Image2D.LoadFromFile($"{Directories.ICONS}/controls/g.png", "ico_g");
        public static Image2D ICO_H = Image2D.LoadFromFile($"{Directories.ICONS}/controls/h.png", "ico_h");
        public static Image2D ICO_I = Image2D.LoadFromFile($"{Directories.ICONS}/controls/i.png", "ico_i");
        public static Image2D ICO_J = Image2D.LoadFromFile($"{Directories.ICONS}/controls/j.png", "ico_j");
        public static Image2D ICO_K = Image2D.LoadFromFile($"{Directories.ICONS}/controls/k.png", "ico_k");
        public static Image2D ICO_L = Image2D.LoadFromFile($"{Directories.ICONS}/controls/l.png", "ico_l");
        public static Image2D ICO_M = Image2D.LoadFromFile($"{Directories.ICONS}/controls/m.png", "ico_m");
        public static Image2D ICO_N = Image2D.LoadFromFile($"{Directories.ICONS}/controls/n.png", "ico_n");
        public static Image2D ICO_O = Image2D.LoadFromFile($"{Directories.ICONS}/controls/o.png", "ico_o");
        public static Image2D ICO_P = Image2D.LoadFromFile($"{Directories.ICONS}/controls/p.png", "ico_p");
        public static Image2D ICO_Q = Image2D.LoadFromFile($"{Directories.ICONS}/controls/q.png", "ico_q");
        public static Image2D ICO_R = Image2D.LoadFromFile($"{Directories.ICONS}/controls/r.png", "ico_r");
        public static Image2D ICO_S = Image2D.LoadFromFile($"{Directories.ICONS}/controls/s.png", "ico_s");
        public static Image2D ICO_T = Image2D.LoadFromFile($"{Directories.ICONS}/controls/t.png", "ico_t");
        public static Image2D ICO_U = Image2D.LoadFromFile($"{Directories.ICONS}/controls/u.png", "ico_u");
        public static Image2D ICO_V = Image2D.LoadFromFile($"{Directories.ICONS}/controls/v.png", "ico_v");
        public static Image2D ICO_W = Image2D.LoadFromFile($"{Directories.ICONS}/controls/w.png", "ico_w");
        public static Image2D ICO_X = Image2D.LoadFromFile($"{Directories.ICONS}/controls/x.png", "ico_x");
        public static Image2D ICO_Y = Image2D.LoadFromFile($"{Directories.ICONS}/controls/y.png", "ico_y");
        public static Image2D ICO_Z = Image2D.LoadFromFile($"{Directories.ICONS}/controls/z.png", "ico_z");

        public static Image2D ICO_0 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/0.png", "ico_0");
        public static Image2D ICO_1 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/1.png", "ico_1");
        public static Image2D ICO_2 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/2.png", "ico_2");
        public static Image2D ICO_3 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/3.png", "ico_3");
        public static Image2D ICO_4 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/4.png", "ico_4");
        public static Image2D ICO_5 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/5.png", "ico_5");
        public static Image2D ICO_6 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/6.png", "ico_6");
        public static Image2D ICO_7 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/7.png", "ico_7");
        public static Image2D ICO_8 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/8.png", "ico_8");
        public static Image2D ICO_9 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/9.png", "ico_9");

        public static Image2D ICO_F1 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f1.png", "ico_f1");
        public static Image2D ICO_F2 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f2.png", "ico_f2");
        public static Image2D ICO_F3 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f3.png", "ico_f3");
        public static Image2D ICO_F4 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f4.png", "ico_f4");
        public static Image2D ICO_F5 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f5.png", "ico_f5");
        public static Image2D ICO_F6 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f6.png", "ico_f6");
        public static Image2D ICO_F7 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f7.png", "ico_f7");
        public static Image2D ICO_F8 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f8.png", "ico_f8");
        public static Image2D ICO_F9 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f9.png", "ico_f9");
        public static Image2D ICO_F10 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f10.png", "ico_f10");
        public static Image2D ICO_F11 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f11.png", "ico_f11");
        public static Image2D ICO_F12 = Image2D.LoadFromFile($"{Directories.ICONS}/controls/f12.png", "ico_f12");

        public static Image2D ICO_EQUALS = Image2D.LoadFromFile($"{Directories.ICONS}/controls/equals.png", "ico_equals");
        public static Image2D ICO_MINUS = Image2D.LoadFromFile($"{Directories.ICONS}/controls/minus.png", "ico_minus");

        public static Image2D ICO_ESC = Image2D.LoadFromFile($"{Directories.ICONS}/controls/esc.png", "ico_esc");
        public static Image2D ICO_TILDE = Image2D.LoadFromFile($"{Directories.ICONS}/controls/tilde.png", "ico_tilde");
        public static Image2D ICO_TAB = Image2D.LoadFromFile($"{Directories.ICONS}/controls/tab.png", "ico_tab");
        public static Image2D ICO_SPACE = Image2D.LoadFromFile($"{Directories.ICONS}/controls/space.png", "ico_space");

        public static Image2D ICO_LEFTSHIFT = Image2D.LoadFromFile($"{Directories.ICONS}/controls/shift_left.png", "ico_leftshift");
        public static Image2D ICO_RIGHTSHIFT = Image2D.LoadFromFile($"{Directories.ICONS}/controls/shift_right.png", "ico_rightshift");
        public static Image2D ICO_LEFTCTRL = Image2D.LoadFromFile($"{Directories.ICONS}/controls/ctrl_left.png", "ico_leftctrl");
        public static Image2D ICO_RIGHTCTRL = Image2D.LoadFromFile($"{Directories.ICONS}/controls/ctrl_right.png", "ico_rightctrl");
        public static Image2D ICO_LEFTALT = Image2D.LoadFromFile($"{Directories.ICONS}/controls/alt_left.png", "ico_leftalt");
        public static Image2D ICO_RIGHTALT = Image2D.LoadFromFile($"{Directories.ICONS}/controls/alt_right.png", "ico_rightalt");

        public static Image2D GetIcon(this Keys key)
        {
            switch (key)
            {
                case Keys.A: return ICO_A;
                case Keys.B: return ICO_B;
                case Keys.C: return ICO_C;
                case Keys.D: return ICO_D;
                case Keys.E: return ICO_E;
                case Keys.F: return ICO_F;
                case Keys.G: return ICO_G;
                case Keys.H: return ICO_H;
                case Keys.I: return ICO_I;
                case Keys.J: return ICO_J;
                case Keys.K: return ICO_K;
                case Keys.L: return ICO_L;
                case Keys.M: return ICO_M;
                case Keys.N: return ICO_N;
                case Keys.O: return ICO_O;
                case Keys.P: return ICO_P;
                case Keys.Q: return ICO_Q;
                case Keys.R: return ICO_R;
                case Keys.S: return ICO_S;
                case Keys.T: return ICO_T;
                case Keys.U: return ICO_U;
                case Keys.V: return ICO_V;
                case Keys.W: return ICO_W;
                case Keys.X: return ICO_X;
                case Keys.Y: return ICO_Y;
                case Keys.Z: return ICO_Z;

                case Keys.D0: return ICO_0;
                case Keys.D1: return ICO_1;
                case Keys.D2: return ICO_2;
                case Keys.D3: return ICO_3;
                case Keys.D4: return ICO_4;
                case Keys.D5: return ICO_5;
                case Keys.D6: return ICO_6;
                case Keys.D7: return ICO_7;
                case Keys.D8: return ICO_8;
                case Keys.D9: return ICO_9;

                case Keys.F1: return ICO_F1;
                case Keys.F2: return ICO_F2;
                case Keys.F3: return ICO_F3;
                case Keys.F4: return ICO_F4;
                case Keys.F5: return ICO_F5;
                case Keys.F6: return ICO_F6;
                case Keys.F7: return ICO_F7;
                case Keys.F8: return ICO_F8;
                case Keys.F9: return ICO_F9;
                case Keys.F10: return ICO_F10;
                case Keys.F11: return ICO_F11;
                case Keys.F12: return ICO_F12;

                case Keys.Equal: return ICO_EQUALS;
                case Keys.Minus: return ICO_MINUS;

                case Keys.Escape: return ICO_ESC;
                case Keys.GraveAccent: return ICO_TILDE;
                case Keys.Tab: return ICO_TAB;
                case Keys.Space: return ICO_SPACE;

                case Keys.LeftShift: return ICO_LEFTSHIFT;
                case Keys.RightShift: return ICO_RIGHTSHIFT;
                case Keys.LeftControl: return ICO_LEFTCTRL;
                case Keys.RightControl: return ICO_RIGHTCTRL;
                case Keys.LeftAlt: return ICO_LEFTALT;
                case Keys.RightAlt: return ICO_RIGHTALT;

                default: return null;
            }
        }
    }
}
