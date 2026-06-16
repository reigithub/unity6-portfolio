#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>
    /// ScriptableTable の CSV/TSV インポート/エクスポート用の、ファイル I/O 非依存な変換ロジック。
    /// セル値の型変換（<see cref="ParseValue"/>/<see cref="FormatValue"/>）と、
    /// ドキュメント文字列 ⇔ 行列（<see cref="ParseDocument"/>/<see cref="WriteDocument"/>）を担う。
    /// 区切り文字を切り替えるだけで CSV/TSV 両対応。CSV はクオート/エスケープ（RFC 4180）に対応する。
    /// 型変換規約は既存 MasterDataHelper.ParseValue（masterdata/raw TSV）に準拠し、InvariantCulture を厳守する。
    /// </summary>
    public static class ScriptableTableTextSerializer
    {
        public const char TabDelimiter = '\t';
        public const char CommaDelimiter = ',';

        /// <summary>拡張子から区切り文字を判定する（.csv はカンマ、それ以外は TAB）。</summary>
        public static char DelimiterFromExtension(string path)
        {
            var ext = Path.GetExtension(path);
            return string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase) ? CommaDelimiter : TabDelimiter;
        }

        // ---- セル値変換 ------------------------------------------------------

        /// <summary>
        /// 文字列セルを指定型へ変換する。string/Nullable/enum(名前)/bool(true|false|1|0)/
        /// 整数・浮動小数・decimal・DateTime・DateTimeOffset・TimeSpan・Guid を InvariantCulture で解釈する。
        /// </summary>
        public static object ParseValue(Type type, string rawValue)
        {
            if (type == typeof(string)) return rawValue;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (string.IsNullOrWhiteSpace(rawValue)) return null;
                return ParseValue(type.GenericTypeArguments[0], rawValue);
            }

            if (type.IsEnum)
            {
                // 基底型へ変換しておく（PropertyInfo.SetValue は enum 型で受けるため値としては等価）。
                var value = Enum.Parse(type, rawValue);
                var underlyingType = Enum.GetUnderlyingType(type);
                return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    // "1"/"0" と "true"/"false" の両方を受理する。
                    if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intBool))
                        return Convert.ToBoolean(intBool);
                    return bool.Parse(rawValue);
                case TypeCode.Char:
                    return char.Parse(rawValue);
                case TypeCode.SByte:
                    return sbyte.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Byte:
                    return byte.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Int16:
                    return short.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.UInt16:
                    return ushort.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Int32:
                    return int.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.UInt32:
                    return uint.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Int64:
                    return long.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.UInt64:
                    return ulong.Parse(rawValue, CultureInfo.InvariantCulture);
                case TypeCode.Single:
                    return float.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture);
                case TypeCode.Double:
                    return double.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return decimal.Parse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture);
                case TypeCode.DateTime:
                    return DateTime.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                default:
                    if (type == typeof(DateTimeOffset))
                        return DateTimeOffset.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    if (type == typeof(TimeSpan))
                        return TimeSpan.Parse(rawValue, CultureInfo.InvariantCulture);
                    if (type == typeof(Guid))
                        return Guid.Parse(rawValue);
                    throw new NotSupportedException($"未対応の型です: {type.FullName}");
            }
        }

        /// <summary>
        /// セル値を文字列へ変換する。<see cref="ParseValue"/> と往復対称になるよう
        /// 浮動小数は往復書式（"R"）、DateTime 系は "O"、bool は小文字、null は空セルとする。
        /// </summary>
        public static string FormatValue(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case string s: return s;
                case bool b: return b ? "true" : "false";
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case decimal m: return m.ToString(CultureInfo.InvariantCulture);
                case DateTime dt: return dt.ToString("O", CultureInfo.InvariantCulture);
                case DateTimeOffset dto: return dto.ToString("O", CultureInfo.InvariantCulture);
                case TimeSpan ts: return ts.ToString("c", CultureInfo.InvariantCulture);
                case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }

        // ---- ドキュメント変換 ------------------------------------------------

        /// <summary>
        /// CSV/TSV テキストをヘッダ行＋データ行へ分解する。空行はスキップする。
        /// CSV（delimiter==','）はクオート対応（"…" 内の区切り・改行・"" エスケープ）。
        /// TSV（delimiter=='\t'）はクオート解釈せず行ごとに単純分割する。
        /// </summary>
        public static (string[] headers, List<string[]> rows) ParseDocument(string text, char delimiter)
        {
            var records = delimiter == CommaDelimiter ? ParseCsv(text) : ParseTsv(text);
            if (records.Count == 0) return (Array.Empty<string>(), new List<string[]>());

            var headers = records[0];
            var rows = new List<string[]>(records.Count - 1);
            for (int i = 1; i < records.Count; i++) rows.Add(records[i]);
            return (headers, rows);
        }

        /// <summary>
        /// ヘッダ＋行を CSV/TSV テキストへ直列化する。
        /// CSV は区切り・改行・引用符を含むセルを "…" でクオートし " を "" にエスケープする。
        /// TSV はエスケープせず素の連結（masterdata/raw 規約）。
        /// </summary>
        public static string WriteDocument(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, char delimiter)
        {
            bool csv = delimiter == CommaDelimiter;
            var sb = new StringBuilder();
            AppendLine(sb, headers, delimiter, csv);
            foreach (var row in rows) AppendLine(sb, row, delimiter, csv);
            return sb.ToString();
        }

        private static void AppendLine(StringBuilder sb, IReadOnlyList<string> cells, char delimiter, bool csv)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(delimiter);
                sb.Append(csv ? EscapeCsvCell(cells[i] ?? string.Empty, delimiter) : cells[i] ?? string.Empty);
            }
            sb.Append('\n');
        }

        private static string EscapeCsvCell(string cell, char delimiter)
        {
            bool needsQuote = cell.IndexOf(delimiter) >= 0 || cell.IndexOf('"') >= 0
                || cell.IndexOf('\n') >= 0 || cell.IndexOf('\r') >= 0;
            if (!needsQuote) return cell;
            return "\"" + cell.Replace("\"", "\"\"") + "\"";
        }

        private static List<string[]> ParseTsv(string text)
        {
            var result = new List<string[]>();
            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                result.Add(line.Split(TabDelimiter));
            }
            return result;
        }

        // RFC 4180 準拠の状態機械。クオート内の区切り・改行・"" を正しく扱う。
        private static List<string[]> ParseCsv(string text)
        {
            var result = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool fieldStarted = false; // 行にセルが1つでも出現したか（空行スキップ判定用）

            void EndField()
            {
                fields.Add(field.ToString());
                field.Clear();
            }

            void EndRecord()
            {
                EndField();
                // 全セルが空の行（区切りも無い）はスキップする。
                bool allEmpty = fields.Count == 1 && fields[0].Length == 0;
                if (!allEmpty) result.Add(fields.ToArray());
                fields.Clear();
                fieldStarted = false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        fieldStarted = true;
                        break;
                    case CommaDelimiter:
                        EndField();
                        fieldStarted = true;
                        break;
                    case '\r':
                        // 続く \n と合わせて 1 改行として扱う。
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                        if (fieldStarted || field.Length > 0) EndRecord();
                        break;
                    case '\n':
                        if (fieldStarted || field.Length > 0) EndRecord();
                        break;
                    default:
                        field.Append(c);
                        fieldStarted = true;
                        break;
                }
            }

            // 末尾改行が無い場合の最終レコード。
            if (fieldStarted || field.Length > 0) EndRecord();
            return result;
        }
    }
}
#endif
