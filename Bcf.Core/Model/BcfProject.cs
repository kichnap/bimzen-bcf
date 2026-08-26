using System;

namespace Bcf.Core.Model
{
    /// <summary>
    /// The project an export belongs to — the project.bcfp file.
    /// Проект, к которому относится выгрузка, — файл project.bcfp.
    /// </summary>
    public class BcfProject
    {
        /// <summary>
        /// The project identifier. A host that has no server-side project may
        /// derive it deterministically from the document path; where a
        /// coordination service exists, it must match the project there.
        ///
        /// Идентификатор проекта. Хост без серверного проекта может выводить
        /// его детерминированно из пути документа; там, где есть сервис
        /// координации, он обязан совпадать с проектом в нём.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// The project name shown to a person.
        /// Имя проекта, которое видит человек.
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// A model the topic refers to — the Header section of the markup.
    /// Модель, к которой относится замечание, — секция Header в разметке.
    /// </summary>
    public class BcfFile
    {
        /// <summary>
        /// The model file name.
        /// Имя файла модели.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// When the model file was produced.
        /// Когда файл модели был получен.
        /// </summary>
        public DateTimeOffset? Date { get; set; }

        /// <summary>
        /// A reference to the file: a URL, or an identifier inside a service.
        /// Ссылка на файл: URL или идентификатор внутри сервиса.
        /// </summary>
        public string Reference { get; set; }

        /// <summary>
        /// The IFC GUID of the project, when the model came from IFC.
        /// IFC GUID проекта, если модель пришла из IFC.
        /// </summary>
        public string IfcProject { get; set; }

        /// <summary>
        /// The IFC GUID of the spatial structure element, when there is one.
        /// IFC GUID элемента пространственной структуры, если он есть.
        /// </summary>
        public string IfcSpatialStructureElement { get; set; }

        /// <summary>
        /// Whether the file lives outside the archive. The schema defaults to true.
        /// Лежит ли файл вне архива. По умолчанию в схеме — true.
        /// </summary>
        public bool IsExternal { get; set; } = true;
    }
}
