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
                FuckingInt = 9876,
                IsShow = true,
                Text = "<%$>$>#$>#@^@>#%>@>%$@>#%>>>>$>#$>@$<@#%^<<^<@>#<>%<@>#%<>@^<>@#^<@>|&<>.,867.5,8.67,64",
                Guid = Guid.NewGuid()
            };
            for (int i = 0; i < 100; i++)
            {
                products.Add
                (new TestListItem
                {
                    Cost = 8976,
                    Name = "<" + 9977534.ToString(CultureInfo.InvariantCulture) + ">",
                    Quantity = 76,
                    Locale = "ru-ru",
                    Duplicate = false
                });
            }
            result.Products = products;
            return result;
        }
    }
}