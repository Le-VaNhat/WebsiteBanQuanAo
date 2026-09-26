using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using ThienThaiShop.Models;
using ThienThaiShop.Services;

namespace ThienThaiShop.Controllers
{
    public class ChatController : Controller
    {
        private readonly ThienThaiDbContext db =
            new ThienThaiDbContext();

        private readonly ChatAiService aiService =
            new ChatAiService();

        // =========================================================
        // SEND MESSAGE
        // =========================================================

        [HttpPost]
        public JsonResult SendMessage(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return Json(new ChatResponseViewModel
                    {
                        Success = false,
                        Message = "Bạn hãy nhập câu hỏi nhé 😊"
                    });
                }

                message = message.Trim();

                if (message.Length > 500)
                    message = message.Substring(0, 500);

                string conversationHistory =
                    GetConversationHistoryText();

                ChatIntent intent =
                    aiService.Analyze(
                        message,
                        conversationHistory
                    );

                ChatResponseViewModel response =
                    BuildResponseFromIntent(
                        message,
                        intent
                    );

                if (response == null)
                {
                    response = new ChatResponseViewModel
                    {
                        Success = true,
                        Message =
                            "Mình chưa hiểu rõ câu hỏi của bạn. " +
                            "Bạn có thể hỏi về sản phẩm, giá, size hoặc tồn kho nhé 😊"
                    };
                }

                SaveConversationTurn(
                    message,
                    response.Message
                );

                return Json(response);
            }
            catch (Exception)
            {
                return Json(new ChatResponseViewModel
                {
                    Success = false,
                    Message =
                        "Xin lỗi, hệ thống chatbot đang gặp sự cố. " +
                        "Bạn thử lại sau một chút nhé."
                });
            }
        }

        // =========================================================
        // BUILD RESPONSE
        // =========================================================

        private ChatResponseViewModel BuildResponseFromIntent(
            string message,
            ChatIntent intent)
        {
            string normalized =
                NormalizeText(message);

            if (intent == null)
            {
                intent =
                    aiService.Analyze(
                        message,
                        GetConversationHistoryText()
                    );

                if (intent == null)
                {
                    return BuildFallbackResponse(message);
                }
            }

            string aiIntent =
                NormalizeIntentName(intent.Intent);

            // -----------------------------------------------------
            // Lấy thông tin chiều cao / cân nặng từ câu hiện tại
            // -----------------------------------------------------

            decimal? currentHeight =
                intent.HeightCm;

            decimal? currentWeight =
                intent.WeightKg;

            decimal? localHeight =
                ExtractHeightLocal(message);

            decimal? localWeight =
                ExtractWeightLocal(message);

            if (!currentHeight.HasValue)
                currentHeight = localHeight;

            if (!currentWeight.HasValue)
                currentWeight = localWeight;

            // -----------------------------------------------------
            // Lưu chiều cao / cân nặng
            // -----------------------------------------------------

            if (currentHeight.HasValue)
                Session["Chat_Height"] =
                    currentHeight.Value;

            if (currentWeight.HasValue)
                Session["Chat_Weight"] =
                    currentWeight.Value;

            // -----------------------------------------------------
            // Category
            // -----------------------------------------------------

            string category =
                NormalizeCategory(intent.Category);

            if (string.IsNullOrWhiteSpace(category))
            {
                string localCategory =
                    DetectCategoryLocal(message);

                if (!string.IsNullOrWhiteSpace(localCategory))
                    category = localCategory;
            }

            // -----------------------------------------------------
            // Size
            // -----------------------------------------------------

            string size =
                NormalizeSize(intent.Size);

            if (string.IsNullOrWhiteSpace(size))
            {
                size =
                    DetectSizeLocal(message);
            }

            // -----------------------------------------------------
            // Giá
            // -----------------------------------------------------

            decimal? minPrice =
                intent.MinPrice;

            decimal? maxPrice =
                intent.MaxPrice;

            if (!minPrice.HasValue)
                minPrice =
                    ExtractMinPriceLocal(message);

            if (!maxPrice.HasValue)
                maxPrice =
                    ExtractMaxPriceLocal(message);

            // -----------------------------------------------------
            // Keyword
            // -----------------------------------------------------

            string keyword =
                intent.Keyword;

            if (string.IsNullOrWhiteSpace(keyword))
                keyword = ExtractKeywordLocal(message);

            // -----------------------------------------------------
            // Xác định loại câu hỏi
            // -----------------------------------------------------

            bool asksGreeting =
                IsGreeting(normalized);

            bool asksThanks =
                IsThanks(normalized);

            bool asksHelp =
                IsHelp(normalized);

            bool asksFollowUp =
                IsFollowUp(normalized);

            // QUAN TRỌNG:
            // Không được dùng height/weight trong Session để
            // tự động biến mọi câu hỏi thành câu hỏi size.
            //
            // Chỉ coi là hỏi size khi:
            // - AI xác định intent=size
            // - hoặc câu hiện tại thực sự có từ khóa hỏi size
            // - hoặc câu hiện tại chỉ cung cấp chiều cao/cân nặng
            bool asksSize =
                aiIntent == "size" ||
                ContainsAny(
                    normalized,
                    "tu van size",
                    "tu van co size",
                    "chon size",
                    "chon co",
                    "size gi",
                    "size nao",
                    "mac size",
                    "mac co",
                    "mặc size",
                    "mặc cỡ",
                    "co vua khong",
                    "vua size",
                    "vua co",
                    "phu hop size"
                );

            bool onlyMeasurement =
                (currentHeight.HasValue ||
                 currentWeight.HasValue) &&
                !ContainsAny(
                    normalized,
                    "gia",
                    "bao nhieu",
                    "duoi",
                    "tren",
                    "tim",
                    "co ao",
                    "co quan",
                    "san pham",
                    "mau",
                    "hang",
                    "con hang",
                    "ton kho"
                ) &&
                !asksGreeting &&
                !asksThanks &&
                !asksHelp;

            if (onlyMeasurement)
                asksSize = true;

            // -----------------------------------------------------
            // Product / price / stock
            // -----------------------------------------------------

            bool asksPrice =
                aiIntent == "price" ||
                minPrice.HasValue ||
                maxPrice.HasValue ||
                ContainsAny(
                    normalized,
                    "gia",
                    "bao nhieu tien",
                    "bao nhieu",
                    "duoi",
                    "tren",
                    "toi da",
                    "toi thieu",
                    "ngan sach",
                    "tam gia"
                );

            bool asksStock =
                aiIntent == "stock" ||
                ContainsAny(
                    normalized,
                    "con hang",
                    "con khong",
                    "ton kho",
                    "het hang",
                    "con size",
                    "con san pham",
                    "co hang"
                );

            bool asksProduct =
                aiIntent == "product" ||
                aiIntent == "search" ||
                asksPrice ||
                asksStock ||
                !string.IsNullOrWhiteSpace(category) ||
                !string.IsNullOrWhiteSpace(size) ||
                !string.IsNullOrWhiteSpace(keyword) ||
                ContainsAny(
                    normalized,
                    "tim",
                    "tim cho",
                    "tim giup",
                    "goi y",
                    "co ao",
                    "co quan",
                    "san pham nao",
                    "mau nao",
                    "mau gi",
                    "shop co",
                    "cho toi xem",
                    "xem san pham"
                );

            // =====================================================
            // GREETING
            // =====================================================

            if (asksGreeting)
            {
                return new ChatResponseViewModel
                {
                    Success = true,
                    Message =
                        "Chào bạn 👋 Mình là trợ lý của Thiên Thai Shop. " +
                        "Bạn có thể hỏi mình về sản phẩm, giá, size hoặc tồn kho nhé!"
                };
            }

            // =====================================================
            // THANKS
            // =====================================================

            if (asksThanks)
            {
                return new ChatResponseViewModel
                {
                    Success = true,
                    Message =
                        "Không có gì ạ 😊 Nếu cần xem thêm sản phẩm hoặc tư vấn size, " +
                        "bạn cứ hỏi mình nhé!"
                };
            }

            // =====================================================
            // HELP
            // =====================================================

            if (asksHelp)
            {
                return new ChatResponseViewModel
                {
                    Success = true,
                    Message =
                        "Bạn có thể hỏi như:\n" +
                        "• Có áo nam nào dưới 400k không?\n" +
                        "• Quần nữ còn size M không?\n" +
                        "• Tôi cao 1m83 nặng 67kg mặc size gì?\n" +
                        "• Sản phẩm này còn hàng không?\n" +
                        "• Cái đầu tiên bao nhiêu tiền?"
                };
            }

            // =====================================================
            // FOLLOW UP
            // =====================================================

            if (asksFollowUp ||
                IsProductReferenceQuestion(normalized))
            {
                return BuildFollowUpResponse(
                    message,
                    normalized
                );
            }

            // =====================================================
            // SIZE
            // =====================================================

            if (asksSize)
            {
                decimal? height =
                    currentHeight;

                decimal? weight =
                    currentWeight;

                if (!height.HasValue)
                {
                    height =
                        GetSessionDecimal("Chat_Height");
                }

                if (!weight.HasValue)
                {
                    weight =
                        GetSessionDecimal("Chat_Weight");
                }

                if (!height.HasValue ||
                    !weight.HasValue)
                {
                    return new ChatResponseViewModel
                    {
                        Success = true,
                        Message =
                            "Để tư vấn size chính xác hơn, bạn cho mình biết " +
                            "chiều cao và cân nặng nhé 😊\n\n" +
                            "Ví dụ: \"Mình cao 1m83, nặng 67kg\"."
                    };
                }

                string recommendedSize =
                    RecommendSize(
                        height.Value,
                        weight.Value
                    );

                if (string.IsNullOrWhiteSpace(recommendedSize))
                {
                    return new ChatResponseViewModel
                    {
                        Success = true,
                        Message =
                            "Mình chưa xác định được size phù hợp. " +
                            "Bạn cho mình biết chiều cao và cân nặng chính xác hơn nhé."
                    };
                }

                Session["Chat_Size"] =
                    recommendedSize;

                List<ChatProductViewModel> products =
                    GetProductsBySize(
                        recommendedSize,
                        category
                    );

                string databaseData =
                    BuildDatabaseProductContext(
                        products
                    );

                string context =
                    BuildConversationContext(
                        height,
                        weight,
                        category,
                        recommendedSize
                    );

                string aiAnswer =
                    aiService.GenerateAnswer(
                        message,
                        context,
                        databaseData
                    );

                if (string.IsNullOrWhiteSpace(aiAnswer))
                {
                    aiAnswer =
                        "Với chiều cao " +
                        FormatNumber(height.Value) +
                        " cm và cân nặng " +
                        FormatNumber(weight.Value) +
                        " kg, bạn có thể tham khảo size " +
                        recommendedSize +
                        " nhé 😊";

                    if (products.Count > 0)
                    {
                        aiAnswer +=
                            "\n\nMình cũng tìm được " +
                            products.Count +
                            " sản phẩm có size " +
                            recommendedSize +
                            " đang còn hàng.";
                    }
                    else
                    {
                        aiAnswer +=
                            "\n\nHiện mình chưa tìm thấy sản phẩm " +
                            "có size " +
                            recommendedSize +
                            " đang còn hàng.";
                    }
                }

                SaveLastProductIds(products);

                return new ChatResponseViewModel
                {
                    Success = true,
                    Message = aiAnswer,
                    Products = products
                };
            }

            // =====================================================
            // PRODUCT / PRICE / STOCK
            // =====================================================

            if (asksProduct ||
                asksPrice ||
                asksStock)
            {
                return BuildProductResponse(
                    message,
                    normalized,
                    category,
                    size,
                    keyword,
                    minPrice,
                    maxPrice,
                    asksStock
                );
            }

            // =====================================================
            // FALLBACK
            // =====================================================

            return BuildFallbackResponse(message);
        }

        // =========================================================
        // PRODUCT RESPONSE
        // =========================================================

        private ChatResponseViewModel BuildProductResponse(
            string message,
            string normalized,
            string category,
            string size,
            string keyword,
            decimal? minPrice,
            decimal? maxPrice,
            bool asksStock)
        {
            // -----------------------------------------------------
            // KHÔNG tự động lấy size cũ ở đây.
            //
            // Đây chính là phần sửa lỗi:
            // "có áo nào dưới 400k không"
            // sẽ không bị lấy Chat_Size cũ.
            // -----------------------------------------------------

            bool explicitFollowUp =
                IsContextFollowUp(normalized);

            if (explicitFollowUp)
            {
                if (string.IsNullOrWhiteSpace(category))
                {
                    category =
                        GetSessionString(
                            "Chat_Category"
                        );
                }

                if (string.IsNullOrWhiteSpace(size))
                {
                    size =
                        GetSessionString(
                            "Chat_Size"
                        );
                }

                if (string.IsNullOrWhiteSpace(keyword))
                {
                    keyword =
                        GetSessionString(
                            "Chat_Keyword"
                        );
                }

                if (!minPrice.HasValue)
                {
                    minPrice =
                        GetSessionDecimal(
                            "Chat_MinPrice"
                        );
                }

                if (!maxPrice.HasValue)
                {
                    maxPrice =
                        GetSessionDecimal(
                            "Chat_MaxPrice"
                        );
                }
            }

            // -----------------------------------------------------
            // Nếu khách nói "áo" nhưng không nói nam/nữ
            // thì không ép vào một category.
            // GetMatchingProducts sẽ tìm cả áo nam + áo nữ.
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(category))
            {
                if (normalized.Contains("ao"))
                    category = null;

                if (normalized.Contains("quan"))
                    category = null;
            }

            // -----------------------------------------------------
            // Lưu câu hiện tại để lớp query có thể phân biệt
            // "áo" / "quần" khi category cụ thể chưa được xác định.
            // -----------------------------------------------------

            Session["Chat_CurrentMessage"] =
                message;

            // -----------------------------------------------------
            // Query database
            // -----------------------------------------------------

            List<ChatProductViewModel> products =
                GetMatchingProducts(
                    category,
                    size,
                    keyword,
                    minPrice,
                    maxPrice,
                    asksStock
                );

            // -----------------------------------------------------
            // Nếu không tìm thấy và đang dùng size cũ từ follow-up
            // thì thử bỏ size để tránh kết quả rỗng không cần thiết.
            // -----------------------------------------------------

            if (products.Count == 0 &&
                explicitFollowUp &&
                !string.IsNullOrWhiteSpace(size))
            {
                products =
                    GetMatchingProducts(
                        category,
                        null,
                        keyword,
                        minPrice,
                        maxPrice,
                        asksStock
                    );
            }

            // -----------------------------------------------------
            // Lưu context hiện tại
            // -----------------------------------------------------

            SaveCurrentContext(
                category,
                size,
                keyword,
                minPrice,
                maxPrice
            );

            SaveLastProductIds(products);

            // -----------------------------------------------------
            // DATABASE DATA cho AI
            // -----------------------------------------------------

            string databaseData =
                BuildDatabaseProductContext(
                    products
                );

            string context =
                BuildConversationContext(
                    GetSessionDecimal("Chat_Height"),
                    GetSessionDecimal("Chat_Weight"),
                    category,
                    size
                );

            // -----------------------------------------------------
            // AI viết câu trả lời
            // -----------------------------------------------------

            string aiAnswer =
                aiService.GenerateAnswer(
                    message,
                    context,
                    databaseData
                );

            // -----------------------------------------------------
            // Fallback nếu OpenAI không trả lời
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(aiAnswer))
            {
                aiAnswer =
                    BuildDeterministicProductMessage(
                        products,
                        category,
                        size,
                        minPrice,
                        maxPrice,
                        asksStock
                    );
            }

            return new ChatResponseViewModel
            {
                Success = true,
                Message = aiAnswer,
                Products = products
            };
        }

        // =========================================================
        // FOLLOW UP
        // =========================================================

        private ChatResponseViewModel BuildFollowUpResponse(
            string message,
            string normalized)
        {
            List<int> lastIds =
                GetLastProductIds();

            if (lastIds.Count == 0)
            {
                return new ChatResponseViewModel
                {
                    Success = true,
                    Message =
                        "Bạn hãy cho mình biết sản phẩm bạn muốn xem trước nhé 😊"
                };
            }

            // -----------------------------------------------------
            // Xác định sản phẩm được hỏi
            // -----------------------------------------------------

            int index =
                ExtractProductIndex(normalized);

            int? referencedProductId = null;

            if (index >= 0 &&
                index < lastIds.Count)
            {
                referencedProductId =
                    lastIds[index];
            }
            else
            {
                int? selectedProductId =
                    GetSelectedProductId();

                if (selectedProductId.HasValue &&
                    lastIds.Contains(selectedProductId.Value))
                {
                    referencedProductId =
                        selectedProductId.Value;
                }
                else if (lastIds.Count == 1)
                {
                    referencedProductId =
                        lastIds[0];
                }
            }

            if (referencedProductId.HasValue)
            {
                int productId =
                    referencedProductId.Value;

                SetSelectedProductId(productId);

                Product product =
                    db.Products
                        .Include("Category")
                        .Include("ProductSizes")
                        .Include("ProductSizes.Size")
                        .FirstOrDefault(
                            p =>
                                p.ProductId == productId &&
                                p.IsActive
                        );

                if (product == null)
                {
                    return new ChatResponseViewModel
                    {
                        Success = true,
                        Message =
                            "Sản phẩm này hiện không còn trên hệ thống."
                    };
                }

                ChatProductViewModel vm =
                    CreateProductViewModel(product);

                string productData =
                    BuildSingleProductContext(
                        product
                    );

                string context =
                    BuildConversationContext(
                        GetSessionDecimal("Chat_Height"),
                        GetSessionDecimal("Chat_Weight"),
                        NormalizeCategory(
                            GetSessionString("Chat_Category")
                        ),
                        NormalizeSize(
                            GetSessionString("Chat_Size")
                        )
                    );

                string aiAnswer =
                    aiService.GenerateAnswer(
                        message,
                        context,
                        productData
                    );

                if (string.IsNullOrWhiteSpace(aiAnswer))
                {
                    aiAnswer =
                        BuildProductStockMessage(
                            product
                        );
                }

                return new ChatResponseViewModel
                {
                    Success = true,
                    Message = aiAnswer,
                    Products =
                        new List<ChatProductViewModel>
                        {
                            vm
                        }
                };
            }

            // -----------------------------------------------------
            // "cho xem thêm"
            // -----------------------------------------------------

            if (ContainsAny(
                normalized,
                "xem them",
                "them san pham",
                "con mau nao",
                "con san pham nao",
                "goi y them"
            ))
            {
                string category =
                    NormalizeCategory(
                        GetSessionString(
                            "Chat_Category"
                        )
                    );

                string size =
                    NormalizeSize(
                        GetSessionString(
                            "Chat_Size"
                        )
                    );

                string keyword =
                    GetSessionString(
                        "Chat_Keyword"
                    );

                decimal? minPrice =
                    GetSessionDecimal(
                        "Chat_MinPrice"
                    );

                decimal? maxPrice =
                    GetSessionDecimal(
                        "Chat_MaxPrice"
                    );

                List<ChatProductViewModel> moreProducts =
                    GetMatchingProducts(
                        category,
                        size,
                        keyword,
                        minPrice,
                        maxPrice,
                        false,
                        lastIds
                    );

                SaveLastProductIds(moreProducts);

                string databaseData =
                    BuildDatabaseProductContext(
                        moreProducts
                    );

                string aiAnswer =
                    aiService.GenerateAnswer(
                        message,
                        BuildConversationContext(
                            GetSessionDecimal("Chat_Height"),
                            GetSessionDecimal("Chat_Weight"),
                            category,
                            size
                        ),
                        databaseData
                    );

                if (string.IsNullOrWhiteSpace(aiAnswer))
                {
                    if (moreProducts.Count > 0)
                    {
                        aiAnswer =
                            "Mình tìm thêm được " +
                            moreProducts.Count +
                            " sản phẩm cho bạn nhé 😊";
                    }
                    else
                    {
                        aiAnswer =
                            "Hiện mình không tìm thêm được sản phẩm phù hợp với điều kiện trước đó.";
                    }
                }

                return new ChatResponseViewModel
                {
                    Success = true,
                    Message = aiAnswer,
                    Products = moreProducts
                };
            }

            if (lastIds.Count > 1 &&
                IsProductReferenceQuestion(normalized))
            {
                return new ChatResponseViewModel
                {
                    Success = true,
                    Message =
                        "Mình đang thấy nhiều sản phẩm. Bạn cho mình biết " +
                        "sản phẩm thứ mấy (ví dụ: \"cái thứ 2\") để mình kiểm tra chính xác nhé 😊"
                };
            }

            return new ChatResponseViewModel
            {
                Success = true,
                Message =
                    "Bạn có thể hỏi mình về giá, size hoặc tồn kho của " +
                    "một trong các sản phẩm vừa hiển thị nhé 😊"
            };
        }

        // =========================================================
        // GET MATCHING PRODUCTS
        // =========================================================

        private List<ChatProductViewModel> GetMatchingProducts(
            string category,
            string size,
            string keyword,
            decimal? minPrice,
            decimal? maxPrice,
            bool onlyInStock,
            List<int> excludeIds = null)
        {
            var query =
                db.Products
                    .Include("Category")
                    .Include("ProductSizes")
                    .Include("ProductSizes.Size")
                    .Where(p => p.IsActive)
                    .AsQueryable();

            // -----------------------------------------------------
            // Category
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(category))
            {
                query =
                    query.Where(
                        p =>
                            p.Category != null &&
                            p.Category.Name == category
                    );
            }
            else
            {
                // Nếu khách nói "áo" -> cả áo nam và áo nữ
                // Nếu khách nói "quần" -> cả quần nam và quần nữ
                string currentMessage =
                    GetSessionString("Chat_CurrentMessage");

                string normalizedCurrent =
                    NormalizeText(currentMessage);

                if (normalizedCurrent.Contains("ao"))
                {
                    query =
                        query.Where(
                            p =>
                                p.Category != null &&
                                (
                                    p.Category.Name == "Áo nam" ||
                                    p.Category.Name == "Áo nữ"
                                )
                        );
                }
                else if (normalizedCurrent.Contains("quan"))
                {
                    query =
                        query.Where(
                            p =>
                                p.Category != null &&
                                (
                                    p.Category.Name == "Quần nam" ||
                                    p.Category.Name == "Quần nữ"
                                )
                        );
                }
            }

            // -----------------------------------------------------
            // Price
            // -----------------------------------------------------

            if (minPrice.HasValue)
            {
                decimal value =
                    minPrice.Value;

                query =
                    query.Where(
                        p => p.Price >= value
                    );
            }

            if (maxPrice.HasValue)
            {
                decimal value =
                    maxPrice.Value;

                query =
                    query.Where(
                        p => p.Price <= value
                    );
            }

            // -----------------------------------------------------
            // Keyword
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string searchKeyword =
                    NormalizeText(keyword);

                query =
                    query.Where(
                        p =>
                            (p.Name != null &&
                             p.Name.ToLower().Contains(searchKeyword)) ||
                            (p.Description != null &&
                             p.Description.ToLower().Contains(searchKeyword))
                    );
            }

            // -----------------------------------------------------
            // Loại sản phẩm về memory
            // để lọc size/tồn kho
            // -----------------------------------------------------

            List<Product> products =
                query
                    .OrderBy(p => p.Price)
                    .ThenBy(p => p.Name)
                    .ToList();

            // -----------------------------------------------------
            // Exclude
            // -----------------------------------------------------

            if (excludeIds != null &&
                excludeIds.Count > 0)
            {
                products =
                    products
                        .Where(
                            p =>
                                !excludeIds.Contains(
                                    p.ProductId
                                )
                        )
                        .ToList();
            }

            // -----------------------------------------------------
            // Size
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(size))
            {
                string normalizedSize =
                    NormalizeSize(size);

                products =
                    products
                        .Where(
                            p =>
                                p.ProductSizes != null &&
                                p.ProductSizes.Any(
                                    ps =>
                                        ps.Size != null &&
                                        ps.Size.Name ==
                                            normalizedSize &&
                                        ps.Stock > 0
                                )
                        )
                        .ToList();
            }

            // -----------------------------------------------------
            // Chỉ còn hàng
            // -----------------------------------------------------

            if (onlyInStock)
            {
                products =
                    products
                        .Where(
                            p =>
                                GetTotalStock(p) > 0
                        )
                        .ToList();
            }

            // -----------------------------------------------------
            // Tối đa 6 sản phẩm
            // -----------------------------------------------------

            return products
                .Take(6)
                .Select(
                    CreateProductViewModel
                )
                .ToList();
        }

        // =========================================================
        // GET PRODUCTS BY SIZE
        // =========================================================

        private List<ChatProductViewModel> GetProductsBySize(
            string size,
            string category)
        {
            var query =
                db.Products
                    .Include("Category")
                    .Include("ProductSizes")
                    .Include("ProductSizes.Size")
                    .Where(
                        p =>
                            p.IsActive &&
                            p.ProductSizes.Any(
                                ps =>
                                    ps.Size != null &&
                                    ps.Size.Name == size &&
                                    ps.Stock > 0
                            )
                    )
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
            {
                query =
                    query.Where(
                        p =>
                            p.Category != null &&
                            p.Category.Name == category
                    );
            }

            List<Product> products =
                query
                    .OrderBy(p => p.Price)
                    .ThenBy(p => p.Name)
                    .Take(6)
                    .ToList();

            return products
                .Select(CreateProductViewModel)
                .ToList();
        }

        // =========================================================
        // PRODUCT VIEW MODEL
        // =========================================================

        private ChatProductViewModel CreateProductViewModel(
            Product product)
        {
            int totalStock =
                GetTotalStock(product);

            return new ChatProductViewModel
            {
                ProductId =
                    product.ProductId,

                Name =
                    product.Name,

                CategoryName =
                    product.Category != null
                        ? product.Category.Name
                        : "",

                Price =
                    product.Price,

                PriceText =
                    product.Price.ToString(
                        "#,##0",
                        CultureInfo.InvariantCulture
                    ) + " ₫",

                ImageUrl =
                    product.ImageUrl,

                TotalStock =
                    totalStock,

                DetailUrl =
                    Url.Action(
                        "Details",
                        "Product",
                        new
                        {
                            id = product.ProductId
                        }
                    )
            };
        }

        // =========================================================
        // DATABASE DATA CHO AI
        // =========================================================

        private string BuildDatabaseProductContext(
            List<ChatProductViewModel> products)
        {
            if (products == null ||
                products.Count == 0)
            {
                return "Không có sản phẩm phù hợp.";
            }

            StringBuilder builder =
                new StringBuilder();

            foreach (ChatProductViewModel product in products)
            {
                builder.AppendLine(
                    "ProductId: " +
                    product.ProductId
                );

                builder.AppendLine(
                    "Name: " +
                    product.Name
                );

                builder.AppendLine(
                    "Category: " +
                    product.CategoryName
                );

                Product contextProduct =
                    db.Products
                        .FirstOrDefault(
                            p => p.ProductId == product.ProductId
                        );

                if (contextProduct != null &&
                    !string.IsNullOrWhiteSpace(contextProduct.Description))
                {
                    builder.AppendLine(
                        "Description: " +
                        contextProduct.Description
                    );
                }

                builder.AppendLine(
                    "Price: " +
                    product.Price.ToString(
                        "#,##0",
                        CultureInfo.InvariantCulture
                    ) +
                    " VND"
                );

                builder.AppendLine(
                    "TotalStock: " +
                    product.TotalStock
                );

                builder.AppendLine(
                    "DetailUrl: " +
                    product.DetailUrl
                );

                Product realProduct =
                    db.Products
                        .Include("ProductSizes")
                        .Include("ProductSizes.Size")
                        .FirstOrDefault(
                            p =>
                                p.ProductId ==
                                product.ProductId
                        );

                if (realProduct != null)
                {
                    builder.AppendLine(
                        "Sizes:"
                    );

                    if (realProduct.ProductSizes != null)
                    {
                        foreach (
                            ProductSize ps
                            in realProduct.ProductSizes
                        )
                        {
                            if (ps.Size == null)
                                continue;

                            builder.AppendLine(
                                "- " +
                                ps.Size.Name +
                                ": " +
                                ps.Stock +
                                " chiếc"
                            );
                        }
                    }
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private string BuildSingleProductContext(
            Product product)
        {
            if (product == null)
                return "Không có sản phẩm.";

            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                "ProductId: " +
                product.ProductId
            );

            builder.AppendLine(
                "Name: " +
                product.Name
            );

            builder.AppendLine(
                "Category: " +
                (
                    product.Category != null
                        ? product.Category.Name
                        : ""
                )
            );

            builder.AppendLine(
                "Price: " +
                product.Price.ToString(
                    "#,##0",
                    CultureInfo.InvariantCulture
                ) +
                " VND"
            );

            builder.AppendLine(
                "TotalStock: " +
                GetTotalStock(product)
            );

            builder.AppendLine(
                "Sizes:"
            );

            if (product.ProductSizes != null)
            {
                foreach (
                    ProductSize ps
                    in product.ProductSizes
                )
                {
                    if (ps.Size == null)
                        continue;

                    builder.AppendLine(
                        "- " +
                        ps.Size.Name +
                        ": " +
                        ps.Stock +
                        " chiếc"
                    );
                }
            }

            return builder.ToString();
        }

        // =========================================================
        // CONVERSATION CONTEXT
        // =========================================================

        private string BuildConversationContext(
            decimal? height,
            decimal? weight,
            string category,
            string size)
        {
            StringBuilder builder =
                new StringBuilder();

            if (height.HasValue)
            {
                builder.AppendLine(
                    "Chiều cao khách: " +
                    FormatNumber(height.Value) +
                    " cm"
                );
            }

            if (weight.HasValue)
            {
                builder.AppendLine(
                    "Cân nặng khách: " +
                    FormatNumber(weight.Value) +
                    " kg"
                );
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                builder.AppendLine(
                    "Category hiện tại: " +
                    category
                );
            }

            if (!string.IsNullOrWhiteSpace(size))
            {
                builder.AppendLine(
                    "Size hiện tại: " +
                    size
                );
            }

            builder.AppendLine();
            builder.AppendLine(
                "LỊCH SỬ HỘI THOẠI GẦN ĐÂY:"
            );
            builder.AppendLine(
                GetConversationSummaryForAi()
            );

            return builder.ToString();
        }

        // =========================================================
        // FALLBACK
        // =========================================================

        private ChatResponseViewModel BuildFallbackResponse(
            string message)
        {
            string normalized =
                NormalizeText(message);

            string category =
                DetectCategoryLocal(message);

            string size =
                DetectSizeLocal(message);

            decimal? minPrice =
                ExtractMinPriceLocal(message);

            decimal? maxPrice =
                ExtractMaxPriceLocal(message);

            bool asksStock =
                ContainsAny(
                    normalized,
                    "con hang",
                    "ton kho",
                    "het hang",
                    "con khong"
                );

            bool asksSize =
                ContainsAny(
                    normalized,
                    "size",
                    "mac co",
                    "tu van size"
                );

            if (asksSize)
            {
                decimal? height =
                    ExtractHeightLocal(message);

                decimal? weight =
                    ExtractWeightLocal(message);

                if (!height.HasValue)
                    height =
                        GetSessionDecimal("Chat_Height");

                if (!weight.HasValue)
                    weight =
                        GetSessionDecimal("Chat_Weight");

                if (height.HasValue &&
                    weight.HasValue)
                {
                    string recommendedSize =
                        RecommendSize(
                            height.Value,
                            weight.Value
                        );

                    List<ChatProductViewModel> products =
                        GetProductsBySize(
                            recommendedSize,
                            category
                        );

                    return new ChatResponseViewModel
                    {
                        Success = true,
                        Message =
                            "Với chiều cao " +
                            FormatNumber(height.Value) +
                            " cm và cân nặng " +
                            FormatNumber(weight.Value) +
                            " kg, bạn có thể tham khảo size " +
                            recommendedSize +
                            " nhé 😊",
                        Products = products
                    };
                }
            }

            List<ChatProductViewModel> result =
                GetMatchingProducts(
                    category,
                    size,
                    null,
                    minPrice,
                    maxPrice,
                    asksStock
                );

            if (result.Count == 0)
            {
                return new ChatResponseViewModel
                {
                    Success = true,
                    Message =
                        "Hiện mình chưa tìm thấy sản phẩm phù hợp với yêu cầu của bạn 😥"
                };
            }

            return new ChatResponseViewModel
            {
                Success = true,
                Message =
                    BuildDeterministicProductMessage(
                        result,
                        category,
                        size,
                        minPrice,
                        maxPrice,
                        asksStock
                    ),
                Products = result
            };
        }

        // =========================================================
        // DETERMINISTIC MESSAGE
        // =========================================================

        private string BuildDeterministicProductMessage(
            List<ChatProductViewModel> products,
            string category,
            string size,
            decimal? minPrice,
            decimal? maxPrice,
            bool asksStock)
        {
            if (products == null ||
                products.Count == 0)
            {
                return "Hiện mình chưa tìm thấy sản phẩm phù hợp.";
            }

            StringBuilder builder =
                new StringBuilder();

            if (maxPrice.HasValue)
            {
                builder.Append(
                    "Mình tìm được " +
                    products.Count +
                    " sản phẩm có giá không quá " +
                    FormatMoney(maxPrice.Value) +
                    " nhé 😊"
                );
            }
            else if (minPrice.HasValue)
            {
                builder.Append(
                    "Mình tìm được " +
                    products.Count +
                    " sản phẩm từ " +
                    FormatMoney(minPrice.Value) +
                    " nhé 😊"
                );
            }
            else if (!string.IsNullOrWhiteSpace(size))
            {
                builder.Append(
                    "Mình tìm được " +
                    products.Count +
                    " sản phẩm còn size " +
                    size +
                    " nhé 😊"
                );
            }
            else if (asksStock)
            {
                builder.Append(
                    "Mình tìm được " +
                    products.Count +
                    " sản phẩm đang còn hàng nhé 😊"
                );
            }
            else
            {
                builder.Append(
                    "Mình tìm được " +
                    products.Count +
                    " sản phẩm phù hợp nhé 😊"
                );
            }

            return builder.ToString();
        }

        // =========================================================
        // STOCK MESSAGE
        // =========================================================

        private string BuildProductStockMessage(
            Product product)
        {
            if (product == null)
                return "Không tìm thấy sản phẩm.";

            int totalStock =
                GetTotalStock(product);

            StringBuilder builder =
                new StringBuilder();

            builder.Append(
                product.Name +
                " hiện có tổng cộng " +
                totalStock +
                " sản phẩm."
            );

            if (product.ProductSizes != null &&
                product.ProductSizes.Count > 0)
            {
                builder.Append(
                    "\n\nTồn kho theo size:"
                );

                foreach (
                    ProductSize ps
                    in product.ProductSizes
                )
                {
                    if (ps.Size == null)
                        continue;

                    builder.Append(
                        "\n• " +
                        ps.Size.Name +
                        ": " +
                        ps.Stock +
                        " chiếc"
                    );
                }
            }

            return builder.ToString();
        }

        // =========================================================
        // TOTAL STOCK
        // =========================================================

        private int GetTotalStock(Product product)
        {
            if (product == null)
                return 0;

            if (product.ProductSizes != null &&
                product.ProductSizes.Count > 0)
            {
                return product.ProductSizes
                    .Sum(
                        ps =>
                            ps.Stock
                    );
            }

            return product.Stock;
        }

        // =========================================================
        // SIZE RECOMMENDATION
        // =========================================================

        private string RecommendSize(
            decimal height,
            decimal weight)
        {
            // Quy tắc đơn giản phù hợp với shop.
            // Có thể điều chỉnh sau theo bảng size thực tế.

            if (height < 160)
            {
                if (weight < 50)
                    return "S";

                if (weight < 60)
                    return "M";

                if (weight < 70)
                    return "L";

                return "XL";
            }

            if (height < 170)
            {
                if (weight < 55)
                    return "S";

                if (weight < 65)
                    return "M";

                if (weight < 75)
                    return "L";

                if (weight < 85)
                    return "XL";

                return "2XL";
            }

            if (height < 180)
            {
                if (weight < 60)
                    return "M";

                if (weight < 70)
                    return "L";

                if (weight < 80)
                    return "XL";

                return "2XL";
            }

            if (weight < 70)
                return "L";

            if (weight < 80)
                return "XL";

            return "2XL";
        }

        // =========================================================
        // CATEGORY
        // =========================================================

        private string NormalizeCategory(
            string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return null;

            string text =
                NormalizeText(category);

            if (text.Contains("ao nam"))
                return "Áo nam";

            if (text.Contains("quan nam"))
                return "Quần nam";

            if (text.Contains("ao nu"))
                return "Áo nữ";

            if (text.Contains("quan nu"))
                return "Quần nữ";

            return null;
        }

        private string DetectCategoryLocal(
            string message)
        {
            string text =
                NormalizeText(message);

            if (text.Contains("ao nam"))
                return "Áo nam";

            if (text.Contains("quan nam"))
                return "Quần nam";

            if (text.Contains("ao nu"))
                return "Áo nữ";

            if (text.Contains("quan nu"))
                return "Quần nữ";

            return null;
        }

        // =========================================================
        // SIZE
        // =========================================================

        private string NormalizeSize(
            string size)
        {
            if (string.IsNullOrWhiteSpace(size))
                return null;

            string text =
                NormalizeText(size);

            if (text == "2xl")
                return "2XL";

            if (text == "xl")
                return "XL";

            if (text == "l")
                return "L";

            if (text == "m")
                return "M";

            if (text == "s")
                return "S";

            return null;
        }

        private string DetectSizeLocal(
            string message)
        {
            string text =
                NormalizeText(message);

            if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])2xl(?![a-z0-9])"
            ))
                return "2XL";

            if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])xl(?![a-z0-9])"
            ))
                return "XL";

            if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])l(?![a-z0-9])"
            ))
                return "L";

            if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])m(?![a-z0-9])"
            ))
                return "M";

            if (Regex.IsMatch(
                text,
                @"(?<![a-z0-9])s(?![a-z0-9])"
            ))
                return "S";

            return null;
        }

        // =========================================================
        // HEIGHT
        // =========================================================

        private decimal? ExtractHeightLocal(
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            string text =
                NormalizeText(message);

            Match match =
                Regex.Match(
                    text,
                    @"(?:1m|1\s*m)\s*(\d{2,3})"
                );

            if (match.Success)
            {
                decimal cm;

                if (decimal.TryParse(
                    match.Groups[1].Value,
                    out cm))
                {
                    return 100 + cm;
                }
            }

            Match cmMatch =
                Regex.Match(
                    text,
                    @"(\d{3})\s*cm"
                );

            if (cmMatch.Success)
            {
                decimal cm;

                if (decimal.TryParse(
                    cmMatch.Groups[1].Value,
                    out cm))
                {
                    return cm;
                }
            }

            return null;
        }

        // =========================================================
        // WEIGHT
        // =========================================================

        private decimal? ExtractWeightLocal(
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            string text =
                NormalizeText(message);

            Match match =
                Regex.Match(
                    text,
                    @"(\d{2,3}(?:[.,]\d+)?)\s*(?:kg|can)"
                );

            if (!match.Success)
                return null;

            decimal value;

            if (decimal.TryParse(
                match.Groups[1].Value.Replace(",", "."),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out value))
            {
                return value;
            }

            return null;
        }

        // =========================================================
        // MAX PRICE
        // =========================================================

        private decimal? ExtractMaxPriceLocal(
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            string text =
                NormalizeText(message);

            Match match =
                Regex.Match(
                    text,
                    @"(?:duoi|toi da|khong qua|tam gia toi da)\s*(\d+(?:[.,]\d+)?)\s*(k|nghin|ngan|trieu|m)?"
                );

            if (!match.Success)
                return null;

            decimal price =
                ConvertPrice(
                    match.Groups[1].Value,
                    match.Groups[2].Value
                );

            if (price <= 0)
                return null;

            return price;
        }

        // =========================================================
        // MIN PRICE
        // =========================================================

        private decimal? ExtractMinPriceLocal(
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            string text =
                NormalizeText(message);

            Match match =
                Regex.Match(
                    text,
                    @"(?:tren|tu|toi thieu|gia tu)\s*(\d+(?:[.,]\d+)?)\s*(k|nghin|ngan|trieu|m)?"
                );

            if (!match.Success)
                return null;

            decimal price =
                ConvertPrice(
                    match.Groups[1].Value,
                    match.Groups[2].Value
                );

            if (price <= 0)
                return null;

            return price;
        }

        // =========================================================
        // CONVERT PRICE
        // =========================================================

        private decimal ConvertPrice(
            string number,
            string unit)
        {
            if (string.IsNullOrWhiteSpace(number))
                return 0;

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
                NormalizeText(unit);

            if (unit == "k" ||
                unit == "nghin" ||
                unit == "ngan")
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

        // =========================================================
        // KEYWORD
        // =========================================================

        private string ExtractKeywordLocal(
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            string text =
                NormalizeText(message);

            string[] ignored =
            {
                "toi",
                "minh",
                "ban",
                "shop",
                "co",
                "cho",
                "tim",
                "tim giup",
                "goi y",
                "san pham",
                "san pham nao",
                "mau",
                "mau nao",
                "ao",
                "quan",
                "nam",
                "nu",
                "gia",
                "bao nhieu",
                "duoi",
                "tren",
                "toi da",
                "toi thieu",
                "con hang",
                "ton kho",
                "het hang"
            };

            string[] words =
                text.Split(
                    new[] { ' ', ',', '.', '?', '!', ':' },
                    StringSplitOptions.RemoveEmptyEntries
                );

            List<string> useful =
                new List<string>();

            foreach (string word in words)
            {
                if (word.Length < 2)
                    continue;

                if (ignored.Contains(word))
                    continue;

                if (Regex.IsMatch(
                    word,
                    @"^\d+(?:[.,]\d+)?$"
                ))
                    continue;

                if (word == "kg" ||
                    word == "can" ||
                    word == "size" ||
                    word == "xl" ||
                    word == "2xl" ||
                    word == "m" ||
                    word == "l" ||
                    word == "s")
                    continue;

                useful.Add(word);
            }

            if (useful.Count == 0)
                return null;

            return string.Join(
                " ",
                useful.Take(3)
            );
        }

        // =========================================================
        // INTENT NAME
        // =========================================================

        private string NormalizeIntentName(
            string intent)
        {
            if (string.IsNullOrWhiteSpace(intent))
                return "other";

            string value =
                NormalizeText(intent);

            if (value == "product")
                return "product";

            if (value == "search")
                return "search";

            if (value == "size")
                return "size";

            if (value == "price")
                return "price";

            if (value == "stock")
                return "stock";

            return "other";
        }

        // =========================================================
        // GREETING
        // =========================================================

        private bool IsGreeting(
            string text)
        {
            return ContainsAny(
                text,
                "xin chao",
                "chao ban",
                "chao shop",
                "hello",
                "hi",
                "hey"
            );
        }

        // =========================================================
        // THANKS
        // =========================================================

        private bool IsThanks(
            string text)
        {
            return ContainsAny(
                text,
                "cam on",
                "thank",
                "thanks",
                "ok cam on",
                "da cam on"
            );
        }

        // =========================================================
        // HELP
        // =========================================================

        private bool IsHelp(
            string text)
        {
            return ContainsAny(
                text,
                "ban lam duoc gi",
                "co the giup gi",
                "huong dan",
                "giup toi",
                "tro giup"
            );
        }

        // =========================================================
        // FOLLOW UP
        // =========================================================

        private bool IsFollowUp(
            string text)
        {
            return ContainsAny(
                text,
                "cai dau tien",
                "cai thu nhat",
                "cai thu 2",
                "cai thu hai",
                "cai thu 3",
                "cai thu ba",
                "cai tren",
                "cai duoi",
                "mau dau",
                "mau thu",
                "san pham dau",
                "san pham tren",
                "san pham duoi",
                "cai nay",
                "mau nay",
                "san pham nay",
                "xem them",
                "them san pham"
            );
        }

        private bool IsContextFollowUp(
            string text)
        {
            return ContainsAny(
                text,
                "cai nay",
                "mau nay",
                "san pham nay",
                "cai tren",
                "cai duoi",
                "xem them",
                "them san pham"
            );
        }

        // =========================================================
        // PRODUCT REFERENCE
        // =========================================================

        private bool IsProductReferenceQuestion(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Các câu hỏi ngắn kiểu hội thoại:
            // "còn size L không?", "giá bao nhiêu?",
            // "mẫu này còn không?", "cái đó size M còn chứ?"
            // cần được hiểu dựa trên sản phẩm vừa chọn/hiển thị.
            if (ContainsAny(
                text,
                "cai nay",
                "cai do",
                "cai kia",
                "mau nay",
                "mau do",
                "mau kia",
                "san pham nay",
                "san pham do",
                "san pham kia",
                "con size",
                "size nao con",
                "size gi con",
                "con khong",
                "con hang khong",
                "bao nhieu tien",
                "gia bao nhieu"
            ))
            {
                return true;
            }

            return false;
        }

        // =========================================================
        // PRODUCT INDEX
        // =========================================================

        private int ExtractProductIndex(
            string text)
        {
            if (ContainsAny(
                text,
                "cai dau tien",
                "cai thu nhat",
                "mau dau",
                "san pham dau"
            ))
                return 0;

            if (ContainsAny(
                text,
                "cai thu 2",
                "cai thu hai",
                "san pham thu 2",
                "mau thu 2"
            ))
                return 1;

            if (ContainsAny(
                text,
                "cai thu 3",
                "cai thu ba",
                "san pham thu 3",
                "mau thu 3"
            ))
                return 2;

            if (ContainsAny(
                text,
                "cai thu 4",
                "san pham thu 4",
                "mau thu 4"
            ))
                return 3;

            if (ContainsAny(
                text,
                "cai thu 5",
                "san pham thu 5",
                "mau thu 5"
            ))
                return 4;

            if (ContainsAny(
                text,
                "cai thu 6",
                "san pham thu 6",
                "mau thu 6"
            ))
                return 5;

            return -1;
        }

        // =========================================================
        // SESSION
        // =========================================================

        private void SaveCurrentContext(
            string category,
            string size,
            string keyword,
            decimal? minPrice,
            decimal? maxPrice)
        {
            Session["Chat_Category"] =
                category;

            Session["Chat_Size"] =
                size;

            Session["Chat_Keyword"] =
                keyword;

            Session["Chat_MinPrice"] =
                minPrice;

            Session["Chat_MaxPrice"] =
                maxPrice;
        }

        private void SaveLastProductIds(
            List<ChatProductViewModel> products)
        {
            if (products == null)
            {
                Session["Chat_LastProductIds"] =
                    new List<int>();

                Session["Chat_SelectedProductId"] =
                    null;

                return;
            }

            Session["Chat_LastProductIds"] =
                products
                    .Select(
                        p => p.ProductId
                    )
                    .ToList();

            // Khi có một danh sách kết quả mới, bỏ lựa chọn sản phẩm cũ.
            Session["Chat_SelectedProductId"] =
                null;

            // Nếu chỉ có một sản phẩm, tự động coi đó là sản phẩm đang được nói tới.
            if (products.Count == 1)
            {
                Session["Chat_SelectedProductId"] =
                    products[0].ProductId;
            }
        }

        private List<int> GetLastProductIds()
        {
            List<int> ids =
                Session["Chat_LastProductIds"]
                as List<int>;

            if (ids != null)
                return ids;

            return new List<int>();
        }

        private int? GetSelectedProductId()
        {
            object value =
                Session["Chat_SelectedProductId"];

            if (value == null)
                return null;

            int id;

            if (int.TryParse(
                value.ToString(),
                out id))
            {
                return id;
            }

            return null;
        }

        private void SetSelectedProductId(
            int productId)
        {
            Session["Chat_SelectedProductId"] =
                productId;
        }

        private string GetConversationHistoryText()
        {
            List<string> history =
                Session["Chat_History"] as List<string>;

            if (history == null || history.Count == 0)
                return "Chưa có lịch sử hội thoại.";

            return string.Join(
                "\n",
                history
            );
        }

        private void SaveConversationTurn(
            string customerMessage,
            string botMessage)
        {
            List<string> history =
                Session["Chat_History"] as List<string>;

            if (history == null)
                history = new List<string>();

            history.Add(
                "KHÁCH: " +
                (customerMessage ?? "")
            );

            history.Add(
                "BOT: " +
                (botMessage ?? "")
            );

            // Chỉ giữ 6 lượt gần nhất để prompt không phình quá lớn.
            const int maxLines = 12;

            if (history.Count > maxLines)
            {
                history =
                    history
                        .Skip(history.Count - maxLines)
                        .ToList();
            }

            Session["Chat_History"] = history;
        }

        private string GetConversationSummaryForAi()
        {
            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                GetConversationHistoryText()
            );

            int? selectedId =
                GetSelectedProductId();

            if (selectedId.HasValue)
            {
                Product selected =
                    db.Products
                        .Include("Category")
                        .Include("ProductSizes")
                        .Include("ProductSizes.Size")
                        .FirstOrDefault(
                            p =>
                                p.ProductId == selectedId.Value &&
                                p.IsActive
                        );

                if (selected != null)
                {
                    builder.AppendLine();
                    builder.AppendLine(
                        "SẢN PHẨM ĐANG ĐƯỢC THAM CHIẾU:"
                    );
                    builder.AppendLine(
                        BuildSingleProductContext(selected)
                    );
                }
            }

            return builder.ToString();
        }

        private string GetSessionString(
            string key)
        {
            object value =
                Session[key];

            if (value == null)
                return null;

            return value.ToString();
        }

        private decimal? GetSessionDecimal(
            string key)
        {
            object value =
                Session[key];

            if (value == null)
                return null;

            if (value is decimal)
                return (decimal)value;

            decimal result;

            if (decimal.TryParse(
                value.ToString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out result))
            {
                return result;
            }

            return null;
        }

        // =========================================================
        // NORMALIZE TEXT
        // =========================================================

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

            return builder
                .ToString()
                .Normalize(
                    NormalizationForm.FormC
                );
        }

        // =========================================================
        // FORMAT
        // =========================================================

        private string FormatNumber(
            decimal value)
        {
            return value.ToString(
                "0.##",
                CultureInfo.InvariantCulture
            );
        }

        private string FormatMoney(
            decimal value)
        {
            return value.ToString(
                "#,##0",
                CultureInfo.InvariantCulture
            ) + " ₫";
        }

        // =========================================================
        // CONTAINS ANY
        // =========================================================

        private bool ContainsAny(
            string text,
            params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                string normalizedValue =
                    NormalizeText(value);

                if (text.Contains(normalizedValue))
                    return true;
            }

            return false;
        }

        // =========================================================
        // DISPOSE
        // =========================================================

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}