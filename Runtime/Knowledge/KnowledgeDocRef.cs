#nullable enable
using UnityEngine;
using SceneBlueprint.Contract.Knowledge;

namespace SceneBlueprint.Runtime.Knowledge
{
    /// <summary>
    /// 知识文档引用。
    /// 将 <see cref="KnowledgeEntry"/> 元数据与 Unity TextAsset（.md 文件）关联。
    /// </summary>
    [System.Serializable]
    public class KnowledgeDocRef
    {
        /// <summary>文档元数据（层级、标题、描述、标签）</summary>
        public KnowledgeEntry Entry = new();

        /// <summary>
        /// 指向 .md 文件的 TextAsset。
        /// 注意：Documentation~ 目录被 Unity 忽略，因此 .md 文件需要放在
        /// 非 ~ 后缀的目录下才能被识别为 TextAsset。
        /// 或者通过自定义 Editor 代码直接读取文件路径。
        /// </summary>
        [Tooltip("指向 .md 知识文档文件")]
        public TextAsset? MarkdownFile;

        /// <summary>
        /// 备用：文件的相对路径（相对于项目根目录）。
        /// 当 MarkdownFile 为 null 时（例如文件在 Documentation~ 目录下），
        /// 通过 System.IO 直接读取此路径。
        /// </summary>
        [Tooltip("备用文件路径（相对于项目根目录），当 TextAsset 不可用时使用")]
        public string FilePath = "";

        /// <summary>
        /// 读取文档内容。优先从 TextAsset 读取，回退到文件路径。
        /// </summary>
        public string? ReadContent()
        {
            if (MarkdownFile != null)
                return MarkdownFile.text;

            if (!string.IsNullOrEmpty(FilePath))
            {
                string fullPath = FilePath;
                if (!System.IO.Path.IsPathRooted(fullPath))
                    fullPath = System.IO.Path.Combine(Application.dataPath, "..", fullPath);

                if (System.IO.File.Exists(fullPath))
                    return System.IO.File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
            }

            return null;
        }
    }
}
