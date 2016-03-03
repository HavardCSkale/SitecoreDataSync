﻿using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Caching;

namespace Sitecore.SharedSource.DataSync.Caching
{
    public class Caching
    {
        private Dictionary<string, List<string>> LocalCache = new Dictionary<string, List<string>>();
        
        public List<string> Get(string key)
        {
            try
            {
                if (LocalCache != null && LocalCache.ContainsKey(key))
                {
                    var cachedValue = LocalCache[key];
                    if (cachedValue != null)
                    {
                        return cachedValue;
                    }
                }
            }
            catch (HttpException ex)
            {
                Sitecore.Diagnostics.Log.Error(string.Format("Fetching: {0}", key), ex);
            }
            return null;
        }

        public void Set(string key, List<string> value)
        {
            if (String.IsNullOrEmpty(key))
            {
                return;
            }
            try
            {
                if (LocalCache != null)
                {
                    if (!LocalCache.ContainsKey(key))
                    {
                        LocalCache.Add(key, value);
                    }
                    else
                    {
                        LocalCache[key] = value;
                    }
                }
            }
            catch (HttpException ex)
            {
                Sitecore.Diagnostics.Log.Error(string.Format("Inserting: {0} into cache failed", key), ex);
                if (LocalCache != null)
                {
                    LocalCache.Remove(key);
                }
            }
        }
    }
}