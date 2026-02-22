# .sbdef 语法高亮 (Windsurf / VS Code)

为 `.sbdef` 文件提供语法高亮支持。

## 一键安装（PowerShell）

```powershell
Copy-Item -Recurse `
  "D:\work\com.zgx197.sceneblueprint\Documentation~\vscode-sbdef" `
  "$env:USERPROFILE\.windsurf\extensions\zgx197.vscode-sbdef-0.1.0"
```

安装后**重启 Windsurf / VS Code** 即可生效。

## 卸载

```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.windsurf\extensions\zgx197.vscode-sbdef-0.1.0"
```

## 打包为 .vsix（可选）

如需通过 UI 安装：

```bash
npm install -g @vscode/vsce
cd Documentation~/vscode-sbdef
vsce package --no-dependencies
# 生成 vscode-sbdef-0.1.0.vsix
# Windsurf/VS Code: Extensions → ··· → Install from VSIX
```
