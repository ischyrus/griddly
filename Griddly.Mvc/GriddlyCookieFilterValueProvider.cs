﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if NET45
using System.Web.Mvc;
#else
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
#endif

namespace Griddly.Mvc
{
    public class GriddlyCookieFilterValueProvider : IValueProvider
    {
        GriddlyContext _context;

        public GriddlyCookieFilterValueProvider(GriddlyContext context)
        {
            _context = context;

            if (_context.CookieData?.SortFields?.Length > 0)
                _context.SortFields = _context.CookieData?.SortFields;
        }

        public bool ContainsPrefix(string prefix)
        {
            return _context.CookieData.Values?.ContainsKey(prefix) == true;
        }

        public ValueProviderResult GetValue(string key)
        {
            string[] value = null;

            if (_context.CookieData.Values?.TryGetValue(key, out value) != true)
                value = null;

            string attemptedValue = null;

            if (value != null)
                attemptedValue = string.Join(",", value);

#if NET45
            return new ValueProviderResult(value, attemptedValue, CultureInfo.CurrentCulture);
#else
            return new ValueProviderResult(value, CultureInfo.CurrentCulture);
#endif
        }
    }

#if NET45
    public class GriddlyCookieFilterValueProviderFactory : ValueProviderFactory
    {
        Func<ControllerContext, bool> _canProvide = null;

        public GriddlyCookieFilterValueProviderFactory(Func<ControllerContext, bool> canProvide = null)
        {
            _canProvide = canProvide;
        }

        public override IValueProvider GetValueProvider(ControllerContext controllerContext)
        {
            if (controllerContext.IsChildAction && controllerContext.HttpContext.Request.QueryString.Count == 0)
            {
                if (_canProvide?.Invoke(controllerContext) != false)
                {
                    var context = controllerContext.Controller.GetOrCreateGriddlyContext();
                    var cookie = controllerContext.HttpContext.Request.Cookies[context.CookieName];

                    if (cookie != null && !string.IsNullOrWhiteSpace(cookie.Value))
                    {
                        try
                        {
                            var data = JsonConvert.DeserializeObject<GriddlyFilterCookieData>(cookie.Value);

                            // chrome/ff don't delete session cookies if they're set to "continue where you left off"
                            // https://stackoverflow.com/questions/10617954/chrome-doesnt-delete-session-cookies
                            // only use a cookie if it's new within 100 minutes
                            if (data.CreatedUtc != null && (DateTime.UtcNow - data.CreatedUtc.Value).TotalMinutes < 100)
                            {
                                context.CookieData = data;
                                context.IsDefaultSkipped = true;

                                return new GriddlyCookieFilterValueProvider(context);
                            }
                        }
                        catch
                        {
                            // TODO: log it?
                        }
                    }
                }
            }

            return null;
        }
    }
#else
    public class GriddlyCookieFilterValueProviderFactory : IValueProviderFactory
    {
        Func<ActionContext, bool> _canProvide = null;

        public GriddlyCookieFilterValueProviderFactory(Func<ActionContext, bool> canProvide = null)
        {
            _canProvide = canProvide;
        }

        public Task CreateValueProviderAsync(ValueProviderFactoryContext vpfc)
        {
            return Task.Factory.StartNew(() =>
            {
                var isChildAction = vpfc.ActionContext.HttpContext.IsChildAction();

                if (isChildAction && vpfc.ActionContext.HttpContext.Request.Query.Count == 0)
                {
                    if (_canProvide?.Invoke(vpfc.ActionContext) != false)
                    {
                        var context = vpfc.ActionContext.GetOrCreateGriddlyContext();
                        var cookie = vpfc.ActionContext.HttpContext.Request.Cookies[context.CookieName];

                        if (cookie != null && !string.IsNullOrWhiteSpace(cookie))
                        {
                            try
                            {
                                var data = JsonConvert.DeserializeObject<GriddlyFilterCookieData>(cookie);

                                // chrome/ff don't delete session cookies if they're set to "continue where you left off"
                                // https://stackoverflow.com/questions/10617954/chrome-doesnt-delete-session-cookies
                                // only use a cookie if it's new within 100 minutes
                                if (data.CreatedUtc != null && (DateTime.UtcNow - data.CreatedUtc.Value).TotalMinutes < 100)
                                {
                                    context.CookieData = data;
                                    context.IsDefaultSkipped = true;

                                    vpfc.ValueProviders.Add(new GriddlyCookieFilterValueProvider(context));
                                }
                            }
                            catch
                            {
                                // TODO: log it?
                            }
                        }
                    }
                }
            });
        }
    }
#endif
}
