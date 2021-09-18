using System.Text;
using System.Text.Json;

namespace Buckets.Web
{
    public class JsonUnderscoreNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            StringBuilder stringBuilder = new();

            foreach (char c in name)
            {
                if (char.IsUpper(c) && stringBuilder.Length != 0) stringBuilder.Append('_');

                stringBuilder.Append(char.ToLower(c));
            }

            return stringBuilder.ToString();
        }
    }
}