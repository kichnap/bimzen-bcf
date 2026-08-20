using System;

namespace Bcf.Core.Model
{
    /// <summary>Проект, к которому относится выгрузка (файл project.bcfp).</summary>
    public class BcfProject
    {
        /// <summary>
        /// Идентификатор проекта. На первом этапе выводится из пути документа
        /// детерминированно, на втором должен совпасть с идентификатором
        /// проекта на площадке.
        /// </summary>
        public string ProjectId { get; set; }

        public string Name { get; set; }
    }

    /// <summary>
    /// Модель, к которой относится замечание (секция Header в markup).
    /// </summary>
    public class BcfFile
    {
        /// <summary>Имя файла модели.</summary>
        public string Filename { get; set; }

        public DateTimeOffset? Date { get; set; }

        /// <summary>Ссылка на файл: URL или внутренний идентификатор на сервере.</summary>
        public string Reference { get; set; }

        /// <summary>IFC GUID проекта, если модель пришла из IFC.</summary>
        public string IfcProject { get; set; }

        public string IfcSpatialStructureElement { get; set; }

        /// <summary>Лежит ли файл вне архива. По умолчанию в схеме — true.</summary>
        public bool IsExternal { get; set; } = true;
    }
}
