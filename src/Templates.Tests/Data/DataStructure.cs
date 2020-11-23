using System;
using System.Collections.Generic;
using System.Linq;

namespace Templates.Tests.Data {
    #region Nested type: TestData

    public class NameValuePair
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class TestData: TestDataStructure {
        public override List<TestListItem> Products {
            get { return ProductsCollection.Where(p => p.Cost > 1000).ToList(); }
            set { ProductsCollection = value; }
        }
    }

    #endregion

    public class Category
    {
        public ComplexObject ComplexObject { get; set; }

        public Category()
        {
            SubCategories = new List<Category>();
        }

        public string Name { get; set; }

        public ICollection<Category> SubCategories { get; set; }
    }
    
    public class DynamicCategory
    {
        public DynamicComplexObject ComplexObject { get; set; }

        public DynamicCategory()
        {
            SubCategories = new List<dynamic>();
        }

        public string Name { get; set; }

        public ICollection<dynamic> SubCategories { get; set; }
    }

    public class ComplexObject
    {
        public TestDataStructure Data { get; set; }
    }
    
    public class DynamicComplexObject
    {
        public DynamicTestDataStructure Data { get; set; }
    }

    #region Nested type: TestDataStructure

    public class TestDataStructure {
        protected List<TestListItem> ProductsCollection;

        public virtual List<TestListItem> Products {
            get { return ProductsCollection; }
            set { ProductsCollection = value; }
        }

        public DateTime Date {
            get;
            set;
        }

        public bool IsShow {
            get;
            set;
        }

        public string Text {
            get;
            set;
        }

        public int FuckingInt {
            get;
            set;
        }

        public Guid Guid {
            get;
            set;
        }
    }
    
    public class DynamicTestDataStructure {
        protected List<dynamic> ProductsCollection;

        public virtual List<dynamic> Products {
            get { return ProductsCollection; }
            set { ProductsCollection = value; }
        }

        public DateTime Date {
            get;
            set;
        }

        public bool IsShow {
            get;
            set;
        }

        public string Text {
            get;
            set;
        }

        public int FuckingInt {
            get;
            set;
        }

        public Guid Guid {
            get;
            set;
        }
    }

    #endregion

    #region Nested type: TestListItem

    public class TestListItem {
        public decimal Cost {
            get;
            set;
        }

        public string Name {
            get;
            set;
        }

        public int Quantity {
            get;
            set;
        }

        public string Locale {
            get;
            set;
        }
    }

    #endregion
}
