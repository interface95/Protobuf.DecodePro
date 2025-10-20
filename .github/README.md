# GitHub Actions 工作流

由于 OAuth 权限限制，工作流文件需要手动添加到 GitHub。

## 如何添加工作流

### 方法 1：直接在 GitHub 网页添加

1. 访问仓库：https://github.com/interface95/Protobuf.DecodePro
2. 点击 **Actions** 标签
3. 点击 **New workflow**
4. 选择 **set up a workflow yourself**
5. 复制下面的工作流内容

### 方法 2：使用 Personal Access Token (推荐)

1. 在 GitHub 生成 PAT：Settings → Developer settings → Personal access tokens → Generate new token
2. 勾选 `repo` 和 `workflow` 权限
3. 使用 PAT 推送：

```bash
# 临时使用 PAT
git remote set-url origin https://YOUR_TOKEN@github.com/interface95/Protobuf.DecodePro.git
git push

# 恢复原来的 URL
git remote set-url origin https://github.com/interface95/Protobuf.DecodePro.git
```

## 工作流文件

### 1. publish-nuget.yml

自动发布到 NuGet（标签触发）

**路径**: `.github/workflows/publish-nuget.yml`

见本地文件：`workflows/publish-nuget.yml`

### 2. publish-parser.yml

手动发布 Parser 包到 NuGet

**路径**: `.github/workflows/publish-parser.yml`

见本地文件：`workflows/publish-parser.yml`

## 使用说明

### 发布到 NuGet

#### 方式 1：通过标签自动发布

```bash
git tag v1.0.0
git push origin v1.0.0
```

#### 方式 2：手动触发

1. 访问 Actions 标签
2. 选择 "Publish Parser to NuGet"
3. 点击 "Run workflow"
4. 输入版本号（如 1.0.0）
5. 点击运行

### 配置 NuGet API Key

在仓库设置中添加 Secret：

1. Settings → Secrets and variables → Actions
2. 点击 "New repository secret"
3. Name: `NUGET_API_KEY`
4. Value: 您的 NuGet API Key

