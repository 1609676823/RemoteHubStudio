# 文件编码与换行规范

本规范可直接复制到其他项目使用，适用于项目维护的源代码、配置、脚本、文档和其他文本文件。默认要求为 **UTF-8 无 BOM + LF**，Windows 批处理脚本和 `.resx` 资源文件采用下述例外。

## 1. 统一要求

| 文件类型 | 编码 | 工作区换行符 | Git 规范化后的存储换行符 |
| --- | --- | --- | --- |
| 普通文本文件，如 `.cs`、`.json`、`.xml`、`.csproj`、`.md`、`.yml`、`.ps1`、`.sh` | UTF-8 无 BOM | LF | LF |
| Windows 批处理脚本：`.bat`、`.cmd` | UTF-8 无 BOM | CRLF | LF |
| Windows/WinForms 资源文件：`.resx` | UTF-8 无 BOM | CRLF | LF |

- **UTF-8 无 BOM**：文件开头不包含 UTF-8 BOM 字节 `EF BB BF`。不要保存为 ANSI、GBK、UTF-16 或 UTF-8 BOM。
- **LF**：换行使用 `\n`；**CRLF**：换行使用 `\r\n`。同一文件不得混用两种换行，也不使用单独的 CR。
- `.resx` 使用 CRLF 是为了匹配 Windows/WinForms 资源写入器，减少设计器反复改写换行产生的差异。
- 图片、图标、压缩包、字体、编译产物等二进制文件不适用本规范，不得按文本转码或转换换行。
- 外部协议或工具明确要求其他编码、换行时，应记录具体文件范围和原因，并同步调整相应配置；不要为少数例外修改整个项目的默认规则。

表中的 Git 存储规则适用于被 Git 识别为文本并执行规范化的文件。工作区使用 CRLF 的例外文件，在 Git 中仍按 LF 存储。

## 2. 编辑器配置：`.editorconfig`

在项目根目录创建以下 `.editorconfig`。如果项目已经有此文件，将这些规则合并进去，保留已有的缩进等设置。

```ini
root = true

[*]
charset = utf-8
end_of_line = lf

[*.{bat,cmd,resx}]
end_of_line = crlf
```

`charset = utf-8` 表示 UTF-8 无 BOM；`utf-8-bom` 才表示带 BOM 的 UTF-8。例外文件只覆盖换行符，仍继承 UTF-8 无 BOM 的编码要求。

编辑器需要支持并启用 EditorConfig。更具体的匹配规则或子目录中的 `.editorconfig` 可能覆盖根目录设置，合并配置时应检查是否存在冲突。

## 3. Git 配置：`.gitattributes`

在仓库根目录创建以下 `.gitattributes`。如果已经存在，将这些规则合并进去，保留已有的 Git LFS、二进制类型及其他属性设置。

```gitattributes
# 默认：自动识别文本，Git 和工作区均使用 LF。
* text=auto eol=lf

# 例外：工作区使用 CRLF，Git 中仍规范化为 LF。
*.bat text eol=crlf
*.cmd text eol=crlf
*.resx text eol=crlf
```

Git 会使用 `text=auto` 自动识别文本，不会把所有文件都作为文本处理。上述显式 `eol` 规则决定适用文件的检出换行方式，因此不需要为本规范修改全局 `core.autocrlf`。

`.gitattributes` 中的这些规则负责换行规范化，**不会验证 UTF-8、移除 BOM 或自动转换其他字符编码**。UTF-8 无 BOM 需要由编辑器和文件生成脚本保证；如需在提交或 CI 中强制检查，应另行添加编码及换行校验。

## 4. 日常编辑与文件生成

1. 新建或修改文本文件时，使用 UTF-8 无 BOM，并按文件类型选择 LF 或 CRLF。
2. 编辑器直接打开 `.resx` 时，也应使用 CRLF，保持编辑器、Git 和设计器的行为一致。
3. 使用脚本或代码生成文件时，显式指定编码和换行符，不依赖操作系统或工具的默认设置。默认文件使用 LF，即使脚本运行在 Windows 上也是如此。
4. 使用 PowerShell 写文件时，注意版本差异：PowerShell 7 可用 `-Encoding utf8NoBOM`；Windows PowerShell 5.1 的 `-Encoding UTF8` 会写入 BOM。编码选项不控制换行，换行需要单独处理。
5. 不要在普通功能修改中顺带重写无关文件的编码或换行；已有文件需要统一时，可单独提交格式变更，便于审查。

本规范只约定编码与换行，不额外规定缩进宽度、行尾空格或文件末尾空行。

## 5. 在其他项目中采用

复制本文作为项目约定后，还需要将第 2、3 节的配置创建或合并到目标项目根目录，并将本文和配置文件纳入版本控制。**仅复制 Markdown 文档，不会自动改变编辑器或 Git 的行为。** `.editorconfig` 和 `.gitattributes` 文件自身也应使用 UTF-8 无 BOM + LF。

对于已有仓库：

1. 检查现有文本文件，按原编码正确读取后转换为 UTF-8 无 BOM，并按本规范调整工作区换行。转换后核对中文、特殊字符和实际内容是否保持一致。
2. 配置生效后，可对已跟踪文件重新执行 Git 换行规范化。先处理好已有修改和暂存内容，再在仓库根目录执行：

   ```shell
   git add --renormalize .
   git diff --cached --stat
   git diff --cached
   ```

   `git add --renormalize .` 会重新暂存已跟踪文件的当前内容，包含这些文件已有的其他修改；不会自动暂存新建的文档或配置文件。它只负责 Git 规范化，不负责字符转码、移除 BOM，也不会直接改写现有工作区文件的换行。检查暂存差异后再提交。

## 6. 检查方法

查看所有受 Git 管理文件的换行状态：

```shell
git ls-files --eol
```

预期结果：

- 普通文本文件通常显示 `i/lf w/lf`。
- `.bat`、`.cmd`、`.resx` 通常显示 `i/lf w/crlf`。
- `i/` 表示 Git 索引中的内容，`w/` 表示工作区内容；`mixed` 表示混合换行，需要处理。
- 空文件或没有换行符的文件可能显示 `none`，二进制文件可能显示 `-text`，这些情况不能直接认定为违规。

查看单个文件的实际 Git 属性时，将下面的路径替换为目标文件：

```shell
git check-attr text eol -- path/to/file
```

`git ls-files --eol` 只检查换行，不验证 UTF-8 或 BOM。编码可通过编辑器的文件编码信息检查；自动校验应严格解码 UTF-8，并检查文件开头是否存在 BOM。
