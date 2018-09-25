// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Docs.Build
{
    internal static class LegacyMetadata
    {
        private static readonly string[] s_metadataBlackList = { "_op_", "fileRelativePath" };

        public static JObject GenerataCommonMetadata(JObject metadata, Docset docset)
        {
            var newMetadata = new JObject(metadata);

            var depotName = $"{docset.Config.Product}.{docset.Config.Name}";
            newMetadata["depot_name"] = depotName;

            newMetadata["search.ms_docsetname"] = docset.Config.Name;
            newMetadata["search.ms_product"] = docset.Config.Product;
            newMetadata["search.ms_sitename"] = "Docs";

            newMetadata["locale"] = docset.Config.Locale;
            newMetadata["site_name"] = "Docs";
            newMetadata["version"] = 0;

            newMetadata["__global"] = new JObject
            {
                ["tutorial_allContributors"] = "all {0} contributors",
            };

            return newMetadata.RemoveNulls();
        }

        public static JObject GenerateLegacyRedirectionRawMetadata(Docset docset, PageModel pageModel)
            => new JObject
            {
                ["redirect_url"] = pageModel.RedirectUrl,
                ["locale"] = docset.Config.Locale,
            }.RemoveNulls();

        public static JObject GenerateLegacyRawMetadata(
                PageModel pageModel,
                string content,
                Docset docset,
                Document file,
                LegacyManifestOutput legacyManifestOutput,
                TableOfContentsMap tocMap)
        {
            var rawMetadata = pageModel.Metadata != null ? JObject.FromObject(pageModel.Metadata) : new JObject();

            rawMetadata = GenerataCommonMetadata(rawMetadata, docset);
            rawMetadata["conceptual"] = content;
            rawMetadata["fileRelativePath"] = legacyManifestOutput.PageOutput.OutputPathRelativeToSiteBasePath.Replace(".raw.page.json", ".html");
            rawMetadata["toc_rel"] = pageModel.TocRel ?? tocMap.FindTocRelativePath(file);

            rawMetadata["wordCount"] = rawMetadata["word_count"] = pageModel.WordCount;

            rawMetadata["title"] = pageModel.Title;
            rawMetadata["rawTitle"] = pageModel.RawTitle ?? "";

            rawMetadata["_op_canonicalUrlPrefix"] = $"{docset.Config.BaseUrl}/{docset.Config.Locale}/{docset.Config.SiteBasePath}/";

            if (docset.Config.NeedGeneratePdfUrlTemplate)
            {
                rawMetadata["_op_pdfUrlPrefixTemplate"] = $"{docset.Config.BaseUrl}/pdfstore/{pageModel.Locale}/{$"{docset.Config.Product}.{docset.Config.Name}"}/{{branchName}}";
            }

            rawMetadata["layout"] = rawMetadata.TryGetValue("layout", out JToken layout) ? layout : "Conceptual";

            rawMetadata["_path"] = PathUtility.NormalizeFile(Path.GetRelativePath(file.Docset.Config.SiteBasePath, file.OutputPath));

            rawMetadata["document_id"] = pageModel.DocumentId;
            rawMetadata["document_version_independent_id"] = pageModel.DocumentVersionIndependentId;

            if (!string.IsNullOrEmpty(pageModel.RedirectUrl))
            {
                rawMetadata["redirect_url"] = pageModel.RedirectUrl;
            }

            if (pageModel.UpdatedAt != default)
            {
                rawMetadata["_op_gitContributorInformation"] = new JObject
                {
                    ["author"] = pageModel.Author?.ToJObject(),
                    ["contributors"] = pageModel.Contributors != null
                        ? new JArray(pageModel.Contributors.Select(c => c.ToJObject()))
                        : null,
                    ["update_at"] = pageModel.UpdatedAt.ToString(docset.Culture.DateTimeFormat.ShortDatePattern, docset.Culture),
                    ["updated_at_date_time"] = pageModel.UpdatedAt,
                };
            }
            if (!string.IsNullOrEmpty(pageModel.Author?.Name))
                rawMetadata["author"] = pageModel.Author?.Name;
            if (pageModel.UpdatedAt != default)
                rawMetadata["updated_at"] = pageModel.UpdatedAt.ToString("yyyy-MM-dd hh:mm tt", docset.Culture);

            rawMetadata["_op_openToPublicContributors"] = docset.Config.Contribution.ShowEdit;

            if (file.ContentType != ContentType.Redirection)
            {
                rawMetadata["open_to_public_contributors"] = docset.Config.Contribution.ShowEdit;

                if (!string.IsNullOrEmpty(pageModel.ContentGitUrl))
                    rawMetadata["content_git_url"] = pageModel.ContentGitUrl;

                if (!string.IsNullOrEmpty(pageModel.Gitcommit))
                    rawMetadata["gitcommit"] = pageModel.Gitcommit;
                if (!string.IsNullOrEmpty(pageModel.OriginalContentGitUrl))
                    rawMetadata["original_content_git_url"] = pageModel.OriginalContentGitUrl;
            }

            return RemoveUpdatedAtDateTime(
                LegacySchema.Transform(
                    docset.Template.TransformMetadata("conceptual", rawMetadata), pageModel)).RemoveNulls();
        }

        public static JObject GenerateLegacyMetadateOutput(JObject rawMetadata)
        {
            var metadataOutput = new JObject();
            foreach (var item in rawMetadata)
            {
                if (!s_metadataBlackList.Any(blackList => item.Key.StartsWith(blackList)))
                {
                    metadataOutput[item.Key] = item.Value;
                }
            }

            metadataOutput["is_dynamic_rendering"] = true;

            return metadataOutput;
        }

        private static JObject RemoveNulls(this JObject graph)
        {
            var (_, jtoken) = ((JToken)graph).RemoveNulls();
            return (JObject)jtoken;
        }

        private static JObject RemoveUpdatedAtDateTime(JObject rawMetadata)
        {
            JToken gitContributorInformation;
            if (rawMetadata.TryGetValue("_op_gitContributorInformation", out gitContributorInformation)
                && ((JObject)gitContributorInformation).ContainsKey("updated_at_date_time"))
            {
                ((JObject)rawMetadata["_op_gitContributorInformation"]).Remove("updated_at_date_time");
            }
            return rawMetadata;
        }

        private static JObject ToJObject(this Contributor info)
        {
            return new JObject
            {
                ["display_name"] = !string.IsNullOrEmpty(info.DisplayName) ? info.DisplayName : info.Name,
                ["id"] = info.Id,
                ["profile_url"] = info.ProfileUrl,
            };
        }
    }
}
