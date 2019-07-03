﻿using System;
using System.ComponentModel;
using System.Resources;

namespace TfsAPI.Attributes
{
    /// <summary>
    /// Локализуемое описание
    /// </summary>
    public class LocalizedDescriptionAttribute : DescriptionAttribute
    {
        ResourceManager _resourceManager;
        string _resourceKey;
        public LocalizedDescriptionAttribute(string resourceKey, Type resourceType = null)
        {
            if (resourceType == null)
            {
                resourceType = typeof(Properties.Resource);
            }

            _resourceManager = new ResourceManager(resourceType);
            _resourceKey = resourceKey;
        }

        public override string Description
        {
            get
            {
                string description = _resourceManager.GetString(_resourceKey);
                return string.IsNullOrWhiteSpace(description) ? string.Format("[[{0}]]", _resourceKey) : description;
            }
        }
    }
}