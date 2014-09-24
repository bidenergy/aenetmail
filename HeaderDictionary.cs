using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace AE.Net.Mail {
	public class HeaderDictionary : SafeDictionary<string, HeaderValue> {
		public HeaderDictionary() : base(StringComparer.OrdinalIgnoreCase) { }

		public virtual string GetBoundary() {
			return this["Content-Type"]["boundary"];
		}

		private static Regex[] rxDates = new[]{
        @"\d{1,2}\s+[a-z]{3}\s+\d{2,4}\s+\d{1,2}\:\d{2}\:\d{1,2}\s+[\+\-\d\:]*",
        @"\d{4}\-\d{1,2}-\d{1,2}\s+\d{1,2}\:\d{2}(?:\:\d{2})?(?:\s+[\+\-\d:]+)?",
      }.Select(x => new Regex(x, RegexOptions.Compiled | RegexOptions.IgnoreCase)).ToArray();

		public virtual DateTime GetDate() {
			var value = this["Date"].RawValue.ToNullDate();
			if (value == null) {
				foreach (var rx in rxDates) {
					var match = rx.Matches(this["Received"].RawValue ?? string.Empty)
						.Cast<Match>().LastOrDefault();
					if (match != null) {
						value = match.Value.ToNullDate();
						if (value != null) {
							break;
						}
					}
				}
			}

			//written this way so a break can be set on the null condition
			if (value == null) return DateTime.MinValue;
			return value.Value;
		}

		public virtual T GetEnum<T>(string name) where T : struct, IConvertible {
			var value = this[name].RawValue;
			if (string.IsNullOrEmpty(value)) return default(T);
			var values = System.Enum.GetValues(typeof(T)).Cast<T>().ToArray();
			return values.FirstOrDefault(x => x.ToString().Equals(value, StringComparison.OrdinalIgnoreCase));
		}

		public virtual void Add(string name, string value) {
			this[name] = new HeaderValue(value);
		}

		public virtual void Add(string name, DateTime value) {
			this[name] = new HeaderValue(value.GetRFC2060Date());
		}

		public virtual MailAddress[] GetMailAddresses(string header) {
			var headerValue = this[header].RawValue.Trim();

		    var mailAddresses = new List<MailAddress>();

            var regexMatches = Regex.Matches(headerValue, @"[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?:[A-Z]{2}|com|org|net|edu|gov|mil|biz|info|mobi|name|aero|asia|jobs|museum)\b"); //email matches
            foreach (var regexMatch in regexMatches)
            {
                var mailAddress = regexMatch.ToString().Trim().ToLower().ToEmailAddress();
                mailAddresses.Add(mailAddress);
            }


			return mailAddresses.Distinct().ToArray();
		}

		public static HeaderDictionary Parse(string headers, System.Text.Encoding encoding) {
			headers = Utilities.DecodeWords(headers, encoding);
			var temp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			var lines = headers.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			int i;
			string key = null, value;
			foreach (var line in lines) {
				if (key != null && (line[0] == '\t' || line[0] == ' ')) {
					temp[key] += line.TrimStartOnce();
				} else {
					if (key != null)
						temp[key] = temp[key].TrimEndOnce(); // It trims the last line of the previous key

					i = line.IndexOf(':');
					if (i > -1) {
						key = line.Substring(0, i).TrimStartOnce();
						value = line.Substring(i + 1).TrimStartOnce();
						temp.Set(key, value);
					}
				}
			}

			var result = new HeaderDictionary();
			foreach (var item in temp) {
				result.Add(item.Key, new HeaderValue(item.Value));
			}
			return result;
		}
	}

}
