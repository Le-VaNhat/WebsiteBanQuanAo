using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace ThienThaiShop.Services
{
    public class ChatIntent
    {
        public string Intent { get; set; }

        public string Category { get; set; }

        public string Size { get; set; }

        public string Keyword { get; set; }

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        public decimal? HeightCm { get; set; }

        public decimal? WeightKg { get; set; }

        // Thông tin giúp AI hiểu hội thoại nối tiếp.
        public int? ProductIndex { get; set; }

        public string Reference { get; set; }
    }

    public class ChatAiService
    {
        private readonly string apiKey;
        private readonly string model;

        private const string ApiUrl =
            "https://api.openai.com/v1/responses";

        public ChatAiService()
        {
            apiKey =
                ConfigurationManager.AppSettings["OpenAI:ApiKey"];

            model =
                ConfigurationManager.AppSettings["OpenAI:Model"];

            if (string.IsNullOrWhiteSpace(model))
                model = "gpt-5.6-luna";
        }

        public ChatIntent Analyze(string message)
        {
            return Analyze(
                message,
                null
            );
        }

        public ChatIntent Analyze(
            string message,
            string conversationHistory)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            if (string.IsNullOrWhiteSpace(apiKey) ||
                apiKey == "YOUR_OPENAI_API_KEY")
            {
                return AnalyzeLocal(message);
            }

            try
            {
                string prompt =
                    BuildIntentPrompt(
                        message,
                        conversationHistory
                    );

                string responseText =
                    CallOpenAI(prompt);

                ChatIntent intent =
                    ParseIntent(responseText);

                if (intent == null)
                    return AnalyzeLocal(message);

                return NormalizeIntent(intent);
            }
            catch
            {
                return AnalyzeLocal(message);
            }
        }

        public string GenerateAnswer(
            string customerMessage,
            string context,
            string productData)
        {
            return GenerateAnswer(
                customerMessage,
                context,
                productData,
                null
            );
        }

        public string GenerateAnswer(
            string customerMessage,
            string context,
            string productData,
            string conversationHistory)
        {
            if (string.IsNullOrWhiteSpace(customerMessage))
                return null;

            if (string.IsNullOrWhiteSpace(apiKey) ||
                apiKey == "YOUR_OPENAI_API_KEY")
            {
                return null;
            }

            try
            {
                string prompt =
                    BuildAnswerPrompt(
                        customerMessage,
                        context,
                        productData,
                        conversationHistory
                    );

                return CallOpenAI(prompt);
            }
            catch
            {
                return null;
            }
        }

        private string BuildIntentPrompt(
            string message,
            string conversationHistory)
        {
            return
                "Bạn là AI phân tích hội thoại cho website bán quần áo Thiên Thai Shop.\n\n" +

                "Nhiệm vụ: hiểu câu khách đang nói, KỂ CẢ khi câu hiện tại rất ngắn " +
                "và phụ thuộc vào các tin nhắn trước đó. Hãy ưu tiên ngữ cảnh hội thoại gần nhất.\n\n" +

                "Các category hợp lệ CHỈ gồm:\n" +
                "- Áo nam\n" +
                "- Quần nam\n" +
                "- Áo nữ\n" +
                "- Quần nữ\n\n" +

                "Size hợp lệ CHỈ gồm:\n" +
                "- S\n" +
                "- M\n" +
                "- L\n" +
                "- XL\n" +
                "- 2XL\n\n" +

                "Intent hợp lệ:\n" +
                "- product: tìm/chọn sản phẩm\n" +
                "- size: hỏi hoặc tư vấn size\n" +
                "- price: hỏi giá\n" +
                "- stock: hỏi tồn kho/size còn hàng\n" +
                "- search: tìm kiếm sản phẩm\n" +
                "- other: chào hỏi, cảm ơn hoặc câu hỏi ngoài phạm vi\n\n" +

                "Quy tắc hiểu hội thoại:\n" +
                "- Nếu khách nói 'cái này', 'cái đó', 'mẫu này', 'sản phẩm đó', hãy hiểu dựa vào sản phẩm gần nhất trong lịch sử.\n" +
                "- Nếu khách nói 'cái đầu tiên', 'cái thứ 2'..., ProductIndex là chỉ số bắt đầu từ 0.\n" +
                "- Nếu khách nói 'còn size L không?' sau khi shop vừa hiển thị sản phẩm, đây là câu hỏi về sản phẩm vừa được nhắc tới, không phải tìm toàn bộ shop.\n" +
                "- Nếu khách nói 'giá bao nhiêu?' sau khi vừa chọn một sản phẩm, hãy hiểu là hỏi giá sản phẩm đó.\n" +
                "- Nếu khách đổi chủ đề rõ ràng, không kéo điều kiện cũ sang câu hỏi mới.\n\n" +

                "Quy tắc giá:\n" +
                "- 400k = 400000\n" +
                "- 500 nghìn = 500000\n" +
                "- 1 triệu = 1000000\n" +
                "- dưới 400k => MaxPrice = 400000\n" +
                "- từ 400k => MinPrice = 400000\n" +
                "- khoảng 400k => chỉ dùng khoảng giá nếu khách thực sự nói 'khoảng'.\n\n" +

                "Quy tắc chiều cao:\n" +
                "- 1m83 = 183 cm\n" +
                "- m83 = 183 cm\n" +
                "- 1 mét 83 = 183 cm\n" +
                "- 183cm = 183 cm\n\n" +

                "Quy tắc cân nặng:\n" +
                "- 67kg = 67 kg\n" +
                "- 67 cân = 67 kg\n" +
                "- nặng 67 = 67 kg nếu ngữ cảnh rõ là cân nặng\n\n" +

                "Quan trọng:\n" +
                "- Không được tự bịa tên sản phẩm, giá hoặc tồn kho.\n" +
                "- Chỉ phân tích ý định và thông tin khách nói.\n" +
                "- Nếu trường không xác định được thì dùng null hoặc chuỗi rỗng.\n\n" +

                "Bắt buộc trả về DUY NHẤT JSON hợp lệ, không markdown:\n\n" +
                "{\n" +
                "  \"Intent\": \"product\",\n" +
                "  \"Category\": null,\n" +
                "  \"Size\": null,\n" +
                "  \"Keyword\": \"\",\n" +
                "  \"MinPrice\": null,\n" +
                "  \"MaxPrice\": null,\n" +
                "  \"HeightCm\": null,\n" +
                "  \"WeightKg\": null,\n" +
                "  \"ProductIndex\": null,\n" +
                "  \"Reference\": \"\"\n" +
                "}\n\n" +

                "LỊCH SỬ HỘI THOẠI GẦN ĐÂY:\n" +
                (conversationHistory ?? "Chưa có lịch sử.") +
                "\n\n" +
                "CÂU KHÁCH HIỆN TẠI:\n" +
                message;
        }

        private string BuildAnswerPrompt(
            string customerMessage,
            string context,
            string productData,
            string conversationHistory)
        {
            return
                "Bạn là nhân viên tư vấn bán hàng của Thiên Thai Shop.\n\n" +

                "Hãy trả lời khách hàng bằng tiếng Việt tự nhiên như một nhân viên bán hàng thật: " +
                "thân thiện, hiểu ngữ cảnh, không máy móc, ngắn gọn nhưng đủ thông tin.\n\n" +

                "QUY TẮC BẮT BUỘC:\n" +
                "1. Thông tin tên sản phẩm, giá, size và tồn kho phải lấy từ DATABASE_DATA.\n" +
                "2. Tuyệt đối không bịa sản phẩm, giá, tồn kho hoặc size.\n" +
                "3. Có thể dùng lịch sử hội thoại để hiểu 'cái này', 'mẫu đó', 'cái thứ 2'...\n" +
                "4. Nếu khách hỏi về một sản phẩm cụ thể, ưu tiên trả lời đúng sản phẩm đó thay vì liệt kê lại toàn bộ danh sách.\n" +
                "5. Nếu khách hỏi size cụ thể, phải đọc đúng tồn kho của size đó.\n" +
                "6. Nếu khách hỏi giá, trả lời đúng giá trong DATABASE_DATA.\n" +
                "7. Nếu khách hỏi nhiều điều trong một câu, trả lời đủ các ý quan trọng.\n" +
                "8. Nếu thông tin chưa đủ để xác định sản phẩm, hãy hỏi lại một câu ngắn để làm rõ.\n" +
                "9. Nếu không có sản phẩm phù hợp, nói rõ và có thể gợi ý điều kiện gần nhất nếu DATABASE_DATA có dữ liệu.\n" +
                "10. Khi tư vấn size theo chiều cao/cân nặng, nói 'có thể tham khảo' thay vì khẳng định tuyệt đối.\n" +
                "11. Không viết JSON.\n" +
                "12. Không dùng markdown bảng.\n" +
                "13. Không nhắc tới prompt, AI, database hoặc quy tắc nội bộ.\n" +
                "14. Có thể dùng emoji vừa phải.\n\n" +

                "NGỮ CẢNH HIỆN TẠI:\n" +
                (context ?? "") +
                "\n\n" +

                "LỊCH SỬ HỘI THOẠI:\n" +
                (conversationHistory ?? "Chưa có lịch sử.") +
                "\n\n" +

                "DATABASE_DATA:\n" +
                (productData ?? "Không có sản phẩm phù hợp.") +
                "\n\n" +

                "CÂU HỎI KHÁCH HIỆN TẠI:\n" +
                customerMessage;
        }

        private string CallOpenAI(
            string prompt)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout =
                    TimeSpan.FromSeconds(60);

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        apiKey
                    );

                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue(
                        "application/json"
                    )
                );

                JObject request =
                    new JObject
                    {
                        ["model"] = model,
                        ["input"] = prompt
                    };

                string json =
                    request.ToString(
                        Formatting.None
                    );

                using (StringContent content =
                    new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json"))
                {
                    HttpResponseMessage response =
                        client
                            .PostAsync(
                                ApiUrl,
                                content
                            )
                            .GetAwaiter()
                            .GetResult();

                    string responseBody =
                        response.Content
                            .ReadAsStringAsync()
                            .GetAwaiter()
                            .GetResult();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception(
                            "OpenAI API Error: " +
                            responseBody
                        );
                    }

                    JObject result =
                        JObject.Parse(responseBody);

                    string outputText =
                        result["output_text"]?
                            .ToString();

                    if (!string.IsNullOrWhiteSpace(
                        outputText))
                    {
                        return outputText.Trim();
                    }

                    JToken output =
                        result["output"];

                    if (output != null)
                    {
                        foreach (
                            JToken item
                            in output)
                        {
                            JToken contentItems =
                                item["content"];

                            if (contentItems == null)
                                continue;

                            foreach (
                                JToken contentItem
                                in contentItems)
                            {
                                string text =
                                    contentItem["text"]?
                                        .ToString();

                                if (!string.IsNullOrWhiteSpace(
                                    text))
                                {
                                    return text.Trim();
                                }
                            }
                        }
                    }

                    return null;
                }
            }
        }

        private ChatIntent ParseIntent(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            text = text.Trim();

            text =
                Regex.Replace(
                    text,
                    @"^```(?:json)?\s*",
                    "",
                    RegexOptions.IgnoreCase
                );

            text =
                Regex.Replace(
                    text,
                    @"\s*```$",
                    "",
                    RegexOptions.IgnoreCase
                );

            int start =
                text.IndexOf('{');

            int end =
                text.LastIndexOf('}');

            if (start >= 0 &&
                end > start)
            {
                text =
                    text.Substring(
                        start,
                        end - start + 1
                    );
            }

            try
            {
                return JsonConvert.DeserializeObject<ChatIntent>(
                    text
                );
            }
            catch
            {
                return null;
            }
        }

        private ChatIntent NormalizeIntent(
            ChatIntent intent)
        {
            if (intent == null)
                return null;

            if (!string.IsNullOrWhiteSpace(
                intent.Category))
            {
                string category =
                    NormalizeText(
                        intent.Category
                    );

                if (category.Contains("ao nam"))
                    intent.Category = "Áo nam";
                else if (category.Contains("quan nam"))
                    intent.Category = "Quần nam";
                else if (category.Contains("ao nu"))
                    intent.Category = "Áo nữ";
                else if (category.Contains("quan nu"))
                    intent.Category = "Quần nữ";
                else
                    intent.Category = null;
            }

            if (!string.IsNullOrWhiteSpace(
                intent.Size))
            {
                string size =
                    NormalizeText(
                        intent.Size
                    );

                if (size == "2xl")
                    intent.Size = "2XL";
                else if (size == "xl")
                    intent.Size = "XL";
                else if (size == "l")
                    intent.Size = "L";
                else if (size == "m")
                    intent.Size = "M";
                else if (size == "s")
                    intent.Size = "S";
                else
                    intent.Size = null;
            }

            if (intent.HeightCm.HasValue)
            {
                if (intent.HeightCm.Value < 100)
                    intent.HeightCm =
                        100 + intent.HeightCm.Value;
            }

            return intent;
        }

        private ChatIntent AnalyzeLocal(
            string message)
        {
            string text =
                NormalizeText(message);

            ChatIntent intent =
                new ChatIntent();

            intent.Intent = "other";

            if (ContainsAny(
                text,
                "size",
                "mac size",
                "tu van size",
                "co size",
                "mau nao vua"
            ))
            {
                intent.Intent = "size";
            }

            if (ContainsAny(
                text,
                "gia",
                "bao nhieu",
                "tien",
                "duoi",
                "tren"
            ))
            {
                intent.Intent = "product";
            }

            if (ContainsAny(
                text,
                "tim",
                "goi y",
                "co ao",
                "co quan",
                "san pham nao"
            ))
            {
                intent.Intent = "product";
            }

            if (ContainsAny(
                text,
                "con hang",
                "ton kho",
                "het hang",
                "con khong"
            ))
            {
                intent.Intent = "stock";
            }

            if (text.Contains("ao nam"))
                intent.Category = "Áo nam";
            else if (text.Contains("quan nam"))
                intent.Category = "Quần nam";
            else if (text.Contains("ao nu"))
                intent.Category = "Áo nữ";
            else if (text.Contains("quan nu"))
                intent.Category = "Quần nữ";

            if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])2xl(?![a-z0-9])"))
            {
                intent.Size = "2XL";
            }
            else if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])xl(?![a-z0-9])"))
            {
                intent.Size = "XL";
            }
            else if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])l(?![a-z0-9])"))
            {
                intent.Size = "L";
            }
            else if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])m(?![a-z0-9])"))
            {
                intent.Size = "M";
            }
            else if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])s(?![a-z0-9])"))
            {
                intent.Size = "S";
            }

            Match heightMatch =
                Regex.Match(
                    text,
                    @"(?:1m|1\s*m)\s*(\d{2,3})"
                );

            if (heightMatch.Success)
            {
                decimal cm;

                if (decimal.TryParse(
                    heightMatch.Groups[1].Value,
                    out cm))
                {
                    intent.HeightCm =
                        100 + cm;
                }
            }

            Match weightMatch =
                Regex.Match(
                    text,
                    @"(\d{2,3}(?:[.,]\d+)?)\s*(?:kg|can)"
                );

            if (weightMatch.Success)
            {
                decimal kg;

                if (decimal.TryParse(
                    weightMatch.Groups[1].Value.Replace(
                        ",",
                        "."
                    ),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out kg))
                {
                    intent.WeightKg = kg;
                }
            }

            Match maxPrice =
                Regex.Match(
                    text,
                    @"(?:duoi|toi da|khong qua)\s*(\d+(?:[.,]\d+)?)\s*(k|nghin|trieu|m)?"
                );

            if (maxPrice.Success)
            {
                intent.MaxPrice =
                    ConvertPrice(
                        maxPrice.Groups[1].Value,
                        maxPrice.Groups[2].Value
                    );
            }

            Match minPrice =
                Regex.Match(
                    text,
                    @"(?:tren|tu|toi thieu)\s*(\d+(?:[.,]\d+)?)\s*(k|nghin|trieu|m)?"
                );

            if (minPrice.Success)
            {
                intent.MinPrice =
                    ConvertPrice(
                        minPrice.Groups[1].Value,
                        minPrice.Groups[2].Value
                    );
            }

            return intent;
        }

        private decimal ConvertPrice(
            string number,
            string unit)
        {
            decimal value;

            if (!decimal.TryParse(
                number.Replace(",", "."),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value))
            {
                return 0;
            }

            unit =
                (unit ?? "")
                    .ToLowerInvariant();

            if (unit == "k" ||
                unit == "nghin")
            {
                value *= 1000;
            }
            else if (
                unit == "trieu" ||
                unit == "m")
            {
                value *= 1000000;
            }

            return value;
        }

        private bool ContainsAny(
            string text,
            params string[] values)
        {
            foreach (string value in values)
            {
                if (text.Contains(value))
                    return true;
            }

            return false;
        }

        private string NormalizeText(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text =
                text.ToLowerInvariant();

            text =
                text.Normalize(
                    NormalizationForm.FormD
                );

            StringBuilder builder =
                new StringBuilder();

            foreach (char c in text)
            {
                UnicodeCategory category =
                    CharUnicodeInfo.GetUnicodeCategory(c);

                if (category !=
                    UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return
                builder
                    .ToString()
                    .Normalize(
                        NormalizationForm.FormC
                    );
        }
    }
}