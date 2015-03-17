using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemplatesXTests.Data {
    #region Nested type: TestData

    public class TestData: TestDataStructure {
        public override List<TestListItem> Products {
            get { return ProductsCollection.Where(p => p.Cost > 1000).ToList(); }
            set { ProductsCollection = value; }
        }
    }

    #endregion

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
