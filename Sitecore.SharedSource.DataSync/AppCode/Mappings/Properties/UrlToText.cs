﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Data.Items;
using System.Data;
using Sitecore.SharedSource.DataSync.Providers;
using Sitecore.Links;
using Sitecore.Data.Fields;

namespace Sitecore.SharedSource.DataSync.Mappings.Properties {
	public class UrlToText : BaseMapping, IBaseProperty {
		#region Properties

		#endregion Properties

		#region Constructor

		//constructor
		public UrlToText(Item i)
			: base(i) {
		}

		#endregion Constructor

		#region Methods

		//fills it's own field
        public void FillField(BaseDataMap map, ref Item newItem, Item importRow)
        {
            Field f = newItem.Fields[NewItemField];
            if(f != null)
                f.Value = LinkManager.GetDynamicUrl(importRow);
		}

		#endregion Methods
	}
}
