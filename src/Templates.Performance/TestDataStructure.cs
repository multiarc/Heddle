using System;
using System.Collections.Generic;
using System.Linq;

namespace Templates.Performance {
    public class NameValuePair
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class TestData: TestDataStructure {
        public override List<TestListItem> Products
        {
            get { return ProductsCollection.Where(p => p.Cost > 1000).ToList(); }
            set { ProductsCollection = value; }
        }
    }

    public class TestListItem {
        public decimal Cost
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public int Quantity
        {
            get;
            set;
        }

        public string Locale
        {
            get;
            set;
        }

        public bool Duplicate { get; set; }

        public TestListItem Inner
        {
            get
            {
                return new TestListItem
                {
                    Cost = Cost,
                    Duplicate = false,
                    Locale = Locale,
                    Name = Name + "DUP",
                    Quantity = Quantity
                };
            }
        }
    }

    public class TestDataStructure {
        protected List<TestListItem> ProductsCollection;

        public virtual List<TestListItem> Products
        {
            get { return ProductsCollection; }
            set { ProductsCollection = value; }
        }

        public DateTime Date
        {
            get;
            set;
        }

        public bool IsShow
        {
            get;
            set;
        }

        public string Text
        {
            get;
            set;
        }

        public int FuckingInt
        {
            get;
            set;
        }

        public Guid Guid
        {
            get;
            set;
        }
    }
}