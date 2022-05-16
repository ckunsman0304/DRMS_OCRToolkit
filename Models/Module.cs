using CountyApps.Entities.Base;
using CountyApps.Entities.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace CountyApps.Entities.Models.Shared
{
    public enum Module
    {
        SHERIFF,
        DELINQUENT_TAXES,
        FRANCHISE_TAXES,
        TITLE,
        LAND,
        MARRIAGE,
        ACCOUNTING,
        DOCUMENT_STORAGE
    }
}