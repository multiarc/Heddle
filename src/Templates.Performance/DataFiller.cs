using System;
using System.Collections.Generic;
using System.Globalization;

namespace Templates.Performance {
    internal static class DataFiller {
        private static readonly Random Rand = new Random(DateTime.Now.Second);

        public static TestDataStructure FillData ()
        {
            var products = new List<TestListItem>();
            var result = new TestDataStructure
            {
                Date = DateTime.Now,
                FuckingInt = Rand.Next(9000, 10000),
                IsShow = Rand.Next(0, 2) == 1,
                Text = "<%$>$>#$>#@^@>#%>@>%$@>#%>>>>$>#$>@$<@#%^<<^<@>#<>%<@>#%<>@^<>@#^<@>|&<>.,867.5,8.67,64",
                Guid = Guid.NewGuid()
            };
            for (int i = 0; i < 10000; i++) {
                products.Add
                    (new TestListItem
                    {
                        Cost = Rand.Next(9000, 10000),
                        Name = "<" + Rand.Next(90000, 100000).ToString(CultureInfo.InvariantCulture) + ">",
                        Quantity = Rand.Next(90, 100),
                        Locale =
                            Rand.Next(0, 5) == 1
                                ? "en-US"
                                : Rand.Next(0, 5) == 2 ? "ru-ru" : Rand.Next(0, 5) == 3 ? "ar-SA" : Rand.Next(0, 5) == 4 ? "th-TH" : "zh-HK",
                        Duplicate = Rand.Next(1, 2) == 1
                    });
            }
            result.Products = products;
            return result;
        }
    }
}