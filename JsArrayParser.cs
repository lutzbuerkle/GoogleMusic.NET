/*
Copyright (c) 2014, Lutz Bürkle
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * Neither the name of the copyright holders nor the
      names of its contributors may be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

// JsArray is partially based on FastJsonParser (https://github.com/ysharplanguage/FastJsonParser)



using System;
using System.Collections;
using System.Globalization;
using System.Text;

namespace Utilities.JsArray
{
    public class JsArray
    {
        private static readonly byte[] HEX = new byte[128];
        private static readonly bool[] HXD = new bool[128];
        private static readonly char[] ESC = new char[128];

        private const int SB_CAPACITY = 256;
        private const int EOF = (char.MaxValue + 1);
        private const int ANY = 0;

        private Func<object>[] parse = new Func<object>[128];
        private StringBuilder sb = new StringBuilder(SB_CAPACITY);
        private string txt;
        private int len;
        private int chr;
        private int at;

        public JsArray()
        {
            parse['n'] = Null;
            parse['f'] = False;
            parse['t'] = True;
            parse['0'] = parse['1'] = parse['2'] = parse['3'] = parse['4'] = parse['5'] = parse['6'] = parse['7'] = parse['8'] = parse['9'] = parse['-'] = Num;
            parse['"'] = Str;
            parse['['] = Arr;
            for (int input = 0; input < 128; input++)
                parse[input] = (parse[input] ?? Error);
        }

        static JsArray()
        {
            for (char c = '0'; c <= '9'; c++)
            {
                HXD[c] = true;
                HEX[c] = (byte)(c - 48);
            }
            for (char c = 'A'; c <= 'F'; c++)
            {
                HXD[c] = HXD[c + 32] = true;
                HEX[c] = HEX[c + 32] = (byte)(c - 55);
            }
            ESC['/'] = '/'; ESC['\\'] = '\\';
            ESC['b'] = '\b'; ESC['f'] = '\f'; ESC['n'] = '\n'; ESC['r'] = '\r'; ESC['t'] = '\t'; ESC['u'] = 'u';
            for (int c = ANY; c < 128; c++)
                if (ESC[c] == ANY) ESC[c] = (char)c;
        }

        public ArrayList Parse(string input)
        {
            at = -1;
            chr = ANY;
            txt = input;
            len = input.Length;

            Space();

            if (chr != '[') throw Error("Array expected");

            return Arr();
        }

        private int Space() { if (chr <= ' ') while ((++at < len) && ((chr = txt[at]) <= ' ')) ; return chr; }

        private int Read() { return (chr = (++at < len) ? txt[at] : EOF); }

        private void Next(int ch) { if (chr != ch) throw Error("Unexpected character"); chr = ((++at < len) ? txt[at] : EOF); }

        private int Char(int ch)
        {
            sb.Append((char)ch);
            return (chr = (++at < len) ? txt[at] : EOF);
        }

        private Exception Error(string message)
        {
            return new Exception(System.String.Format("{0} at {1} (found: '{2}')", message, at, ((chr < EOF) ? ("\\" + chr) : "EOF")));
        }

        private object Value()
        {
            return parse[Space() & 0x7f]();
        }

        private object Error() { throw Error("Bad value"); }

        private object Null() { Read(); Next('u'); Next('l'); Next('l'); return null; }

        private object False() { Read(); Next('a'); Next('l'); Next('s'); Next('e'); return false; }

        private object True() { Read(); Next('r'); Next('u'); Next('e'); return true; }

        private object Num()
        {
            int ch = chr;
            bool valid = false;
            bool dec = false;
            sb.Length = 0;

            if (ch == '-') ch = Char(ch);

            while ((ch >= '0') && (ch <= '9') && (valid = true)) ch = Char(ch);

            if (ch == '.')
            {
                dec = true;
                ch = Char(ch);
                while ((ch >= '0') && (ch <= '9')) ch = Char(ch);
            }

            if ((ch == 'e') || (ch == 'E'))
            {
                ch = Char(ch);
                if ((ch == '-') || (ch == '+')) ch = Char(ch);
                while ((ch >= '0') && (ch <= '9')) ch = Char(ch);
            }

            if (!valid) throw Error("Bad number");

            if (dec)
                return Double.Parse(sb.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture);
            else
                return Int64.Parse(sb.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        private string Str()
        {
            var ch = Space();
            var e = false;

            if (ch == '"')
            {
                Read();
                sb.Length = 0;
                while (true)
                {
                    if ((ch = chr) == '"')
                    {
                        Read();
                        return (sb.ToString());
                    }
                    if (e = (ch == '\\')) ch = Read();
                    if (ch < EOF)
                    {
                        if (!e || (ch >= 128))
                            Char(ch);
                        else
                        {
                            CharEsc(ch);
                            e = false;
                        }
                    }
                    else
                        break;
                }
            }

            throw Error("Bad string");
        }

        private ArrayList Arr()
        {
            ArrayList array = new ArrayList();
            int ch = chr;

            if (ch == '[')
            {
                Read();
                ch = Space();
                if (ch == ']')
                {
                    Read();
                    return array;
                }
                while (ch < EOF)
                {
                    if (ch == ',')
                    {
                        array.Add(null);
                    }
                    else
                    {
                        array.Add(Value());
                        ch = Space();
                        if (ch == ']')
                        {
                            Read();
                            return array;
                        }
                    }
                    Next(',');
                    ch = Space();
                    if (ch == ']')
                    {
                        array.Add(null);
                        Read();
                        return array;
                    }
                }
            }

            throw Error("Bad array");
        }

        private int CharEsc(int ec)
        {
            int cp = 0, ic = -1, ch;
            if (ec == 'u')
            {
                while ((++ic < 4) && ((ch = Read()) <= 'f') && HXD[ch]) { cp *= 16; cp += HEX[ch]; }
                if (ic < 4) throw Error("Invalid Unicode character");
                ch = cp;
            }
            else
                ch = ESC[ec];
            Char(ch);

            return ch;
        }
    }


    public static class JsArrayExtensionMethod
    {
        static StringBuilder _s;

        public static string ToJsArray(this ArrayList input)
        {
            _s = new StringBuilder();

            ProcessArray(input);

            return _s.ToString();
        }

        private static void ProcessArray(ArrayList array)
        {
            bool init = false;

            _s.Append('[');
            foreach (object element in array)
            {
                if (init)
                    _s.Append(',');
                else
                    init = true;

                if (element == null)
                    _s.Append("null");
                else if (element is ArrayList)
                    ProcessArray(element as ArrayList);
                else if (element is String)
                {
                    _s.Append('"');
                    _s.Append(element as String);
                    _s.Append('"');
                }
                else if (element is Boolean)
                {
                    if ((Boolean)element)
                        _s.Append("true");
                    else
                        _s.Append("false");
                }
                else
                {
                    switch (Type.GetTypeCode(element.GetType()))
                    {
                        case TypeCode.Byte:
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            _s.Append((UInt64)Convert.ChangeType(element, TypeCode.UInt64));
                            break;
                        case TypeCode.SByte:
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                            _s.Append((Int64)Convert.ChangeType(element, TypeCode.Int64));
                            break;
                        case TypeCode.Decimal:
                        case TypeCode.Double:
                        case TypeCode.Single:
                            _s.Append(((Double)Convert.ChangeType(element, TypeCode.Double)).ToString("0.0#", new CultureInfo("en-US")));
                            break;
                        default:
                            throw new Exception("Unsupported data type in ArrayList");
                    }
                }
            }
            _s.Append(']');
        }
    }
}
