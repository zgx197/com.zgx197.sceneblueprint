#nullable enable
using SceneBlueprint.Editor.Markers;
using SceneBlueprint.Editor.WindowServices;

namespace SceneBlueprint.Editor.WindowServices.Binding
{
    /// <summary>
    /// 场景绑定验证器——执行标记绑定一致性检查（缺失/孤立/类型不匹配），结果输出到 Console。
    /// </summary>
    public class SceneBindingValidator
    {
        private readonly IBlueprintReadContext _ctx;

        public SceneBindingValidator(IBlueprintReadContext ctx) => _ctx = ctx;

        public ValidationReport RunValidation()
        {
            var vm = _ctx.ViewModel;
            if (vm == null) return new ValidationReport();
            var report = MarkerBindingValidator.Validate(vm.Graph, _ctx.ActionRegistry);
            MarkerBindingValidator.LogReport(report);
            return report;
        }
    }
}
