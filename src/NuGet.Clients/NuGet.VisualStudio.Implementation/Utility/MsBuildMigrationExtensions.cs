﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using NuGet.Frameworks;

namespace NuGet.VisualStudio.Migration
{
    public static class MsBuildMigrationExtensions
    {
        public static IEnumerable<string> GetEncompassedIncludes(this ProjectItemElement item,
            ProjectItemElement otherItem, TextWriter trace = null)
        {
            if (otherItem.IsEquivalentToExceptIncludeUpdateAndExclude(item, trace) &&
                new HashSet<string>(otherItem.Excludes()).IsSubsetOf(new HashSet<string>(item.Excludes())))
            {
                return otherItem.IntersectIncludes(item);
            }

            return Enumerable.Empty<string>();
        }

        public static IEnumerable<string> GetEncompassedUpdates(this ProjectItemElement item,
            ProjectItemElement otherItem, TextWriter trace = null)
        {
            if (otherItem.IsEquivalentToExceptIncludeUpdateAndExclude(item, trace) &&
                new HashSet<string>(otherItem.Excludes()).IsSubsetOf(new HashSet<string>(item.Excludes())))
            {
                return otherItem.IntersectUpdates(item);
            }

            return Enumerable.Empty<string>();
        }

        public static bool IsEquivalentTo(this ProjectItemElement item, ProjectItemElement otherItem, TextWriter trace = null)
        {
            // Different includes
            if (item.IntersectIncludes(otherItem).Count() != item.Includes().Count())
            {
                return false;
            }

            if (item.IntersectUpdates(otherItem).Count() != item.Updates().Count())
            {
                return false;
            }

            // Different Excludes
            if (item.IntersectExcludes(otherItem).Count() != item.Excludes().Count())
            {
                return false;
            }

            return item.IsEquivalentToExceptIncludeUpdateAndExclude(otherItem, trace);
        }

        public static bool IsEquivalentToExceptIncludeUpdateAndExclude(this ProjectItemElement item, ProjectItemElement otherItem, TextWriter trace = null)
        {
            // Different remove
            if (item.Remove != otherItem.Remove)
            {
                return false;
            }

            // Different Metadata
            var metadataTuples = otherItem.Metadata.Select(m => Tuple.Create(m, item)).Concat(
                item.Metadata.Select(m => Tuple.Create(m, otherItem)));
            foreach (var metadataTuple in metadataTuples)
            {
                var metadata = metadataTuple.Item1;
                var itemToCompare = metadataTuple.Item2;

                var otherMetadata = itemToCompare.GetMetadataWithName(metadata.Name);
                if (otherMetadata == null)
                {
                    return false;
                }

                if (!metadata.ValueEquals(otherMetadata))
                {
                    return false;
                }
            }

            return true;
        }

        public static ISet<string> ConditionChain(this ProjectElement projectElement)
        {
            var conditionChainSet = new HashSet<string>();

            if (!string.IsNullOrEmpty(projectElement.Condition))
            {
                conditionChainSet.Add(projectElement.Condition);
            }

            foreach (var parent in projectElement.AllParents)
            {
                if (!string.IsNullOrEmpty(parent.Condition))
                {
                    conditionChainSet.Add(parent.Condition);
                }
            }

            return conditionChainSet;
        }

        public static bool ConditionChainsAreEquivalent(this ProjectElement projectElement, ProjectElement otherProjectElement)
        {
            return projectElement.ConditionChain().SetEquals(otherProjectElement.ConditionChain());
        }

        public static IEnumerable<ProjectPropertyElement> PropertiesWithoutConditions(
            this ProjectRootElement projectRoot)
        {
            return ElementsWithoutConditions(projectRoot.Properties);
        }

        public static IEnumerable<ProjectItemElement> ItemsWithoutConditions(
            this ProjectRootElement projectRoot)
        {
            return ElementsWithoutConditions(projectRoot.Items);
        }

        public static IEnumerable<string> Includes(
            this ProjectItemElement item)
        {
            return SplitSemicolonDelimitedValues(item.Include);
        }

        public static IEnumerable<string> Updates(
            this ProjectItemElement item)
        {
            return SplitSemicolonDelimitedValues(item.Update);
        }

        public static IEnumerable<string> Excludes(
            this ProjectItemElement item)
        {
            return SplitSemicolonDelimitedValues(item.Exclude);
        }

        public static IEnumerable<string> Removes(
            this ProjectItemElement item)
        {
            return SplitSemicolonDelimitedValues(item.Remove);
        }

        public static IEnumerable<string> AllConditions(this ProjectElement projectElement)
        {
            return new string[] { projectElement.Condition }.Concat(projectElement.AllParents.Select(p => p.Condition));
        }

        public static IEnumerable<string> IntersectIncludes(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            return item.Includes().Intersect(otherItem.Includes());
        }

        public static IEnumerable<string> IntersectUpdates(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            return item.Updates().Intersect(otherItem.Updates());
        }

        public static IEnumerable<string> IntersectExcludes(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            return item.Excludes().Intersect(otherItem.Excludes());
        }

        public static void RemoveIncludes(this ProjectItemElement item, IEnumerable<string> includesToRemove)
        {
            item.Include = string.Join(";", item.Includes().Except(includesToRemove));
        }

        public static void RemoveUpdates(this ProjectItemElement item, IEnumerable<string> updatesToRemove)
        {
            item.Update = string.Join(";", item.Updates().Except(updatesToRemove));
        }

        public static void UnionIncludes(this ProjectItemElement item, IEnumerable<string> includesToAdd)
        {
            item.Include = string.Join(";", item.Includes().Union(includesToAdd));
        }

        public static void UnionExcludes(this ProjectItemElement item, IEnumerable<string> excludesToAdd)
        {
            item.Exclude = string.Join(";", item.Excludes().Union(excludesToAdd));
        }

        public static bool ValueEquals(this ProjectMetadataElement metadata, ProjectMetadataElement otherMetadata)
        {
            return metadata.Value.Equals(otherMetadata.Value, StringComparison.Ordinal);
        }

        public static void AddMetadata(this ProjectItemElement item, ICollection<ProjectMetadataElement> metadataElements, TextWriter trace = null)
        {
            foreach (var metadata in metadataElements)
            {
                item.AddMetadata(metadata, trace);
            }
        }

        public static void RemoveIfEmpty(this ProjectElementContainer container)
        {
            if (!container.Children.Any())
            {
                container.Parent.RemoveChild(container);
            }
        }

        public static ProjectMetadataElement GetMetadataWithName(this ProjectItemElement item, string name)
        {
            return item.Metadata.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static void AddMetadata(this ProjectItemElement item, ProjectMetadataElement metadata, TextWriter trace = null)
        {
            var existingMetadata = item.GetMetadataWithName(metadata.Name);

            if (existingMetadata != default(ProjectMetadataElement) && !existingMetadata.ValueEquals(metadata))
            {
                throw new Exception("Cannot merge metadata");
            }

            if (existingMetadata == default(ProjectMetadataElement))
            {
                var metametadata = item.AddMetadata(metadata.Name, metadata.Value);
                metametadata.Condition = metadata.Condition;
                metametadata.ExpressedAsAttribute = metadata.ExpressedAsAttribute;
            }
        }

        public static void SetExcludeOnlyIfIncludeIsSet(this ProjectItemElement item, string exclude)
        {
            item.Exclude = string.IsNullOrEmpty(item.Include) ? string.Empty : exclude;
        }

        private static IEnumerable<string> SplitSemicolonDelimitedValues(string combinedValue)
        {
            return string.IsNullOrEmpty(combinedValue) ? Enumerable.Empty<string>() : combinedValue.Split(';');
        }

        private static IEnumerable<T> ElementsWithoutConditions<T>(IEnumerable<T> elements) where T : ProjectElement
        {
            return elements
                .Where(e => string.IsNullOrEmpty(e.Condition)
                            && e.AllParents.All(parent => string.IsNullOrEmpty(parent.Condition)));
        }

        public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> enumerable)
        {
            return enumerable == null
                ? Enumerable.Empty<T>()
                : enumerable;
        }

        public static string GetMSBuildCondition(this NuGetFramework framework)
        {
            return $" '$(TargetFramework)' == '{framework.GetTwoDigitShortFolderName()}' ";
        }

        public static string GetTwoDigitShortFolderName(this NuGetFramework self)
        {
            var original = self.GetShortFolderName();
            var index = 0;
            for (; index < original.Length; index++)
            {
                if (char.IsDigit(original[index]))
                {
                    break;
                }
            }

            var versionPart = original.Substring(index);
            if (versionPart.Length >= 2)
            {
                return original;
            }

            // Assume if the version part was preserved then leave it alone
            if (versionPart.IndexOf('.') != -1)
            {
                return original;
            }

            var name = original.Substring(0, index);
            var version = self.Version.ToString(2);

            if (self.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.NetPlatform))
            {
                return name + version;
            }

            return name + version.Replace(".", string.Empty);
        }
    }
}
