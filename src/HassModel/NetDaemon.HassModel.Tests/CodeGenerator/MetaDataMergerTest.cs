﻿using NetDaemon.HassModel.CodeGenerator;

namespace NetDaemon.HassModel.Tests.CodeGenerator;

public class MetaDataMergerTest
{
    [Fact]
    public void MergeSimple()
    {
        var previous = new []{new EntityDomainMetadata("light", false, new []
            {
                new EntityMetaData("light.living", "Livingroom spots", "Living"),
                new EntityMetaData("light.kitchen", "Kitchen light", "Kitchen")
            },
            new []
            {
                new EntityAttributeMetaData("brightness", "Brightness", typeof(double))
            })};

        var current = new []{new EntityDomainMetadata("light", false, new []
            {
                new EntityMetaData("light.bedroom", "nightlight", "Bedroom"),
                new EntityMetaData("light.kitchen", "Kitchen light new name", "Kitchen")
            },
            new []
            {
                new EntityAttributeMetaData("off_brightness", "OffBrightness", typeof(double))
            })};

        var result = EntityMetaDataMerger.Merge(new(), new EntitiesMetaData { Domains = previous }, new EntitiesMetaData { Domains = current }).Domains;

        var expected = new []{new EntityDomainMetadata("light", false, new []
            {
                new EntityMetaData("light.bedroom", "nightlight", "Bedroom"),
                new EntityMetaData("light.kitchen", "Kitchen light new name", "Kitchen")
            },
            new []
            {
                new EntityAttributeMetaData("brightness", "Brightness", typeof(double)),
                new EntityAttributeMetaData("off_brightness", "OffBrightness", typeof(double))
            })};

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void DeduplictateCSharpName()
    {
        var previous = new [] { new EntityAttributeMetaData("bright_ness", "Brightness", typeof(double)) };

        var current = new [] { new EntityAttributeMetaData("brightness", "Brightness", typeof(double)) };

        var result = TestAttributeMerge(previous, current);

        var expected = new [] {
                new EntityAttributeMetaData("bright_ness", "Brightness_0", typeof(double)),
                new EntityAttributeMetaData("brightness", "Brightness_1", typeof(double)),
            };

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void FixedCSharpName()
    {
        var previous = new [] { new EntityAttributeMetaData("Happyness", "JoyJoy", typeof(double)) };
        var current = new [] { new EntityAttributeMetaData("Happyness", "Happyness", typeof(double)) };

        var expected = new [] { new EntityAttributeMetaData("Happyness", "JoyJoy", typeof(double)), };

        var result = TestAttributeMerge(previous, current);
        result.Should().BeEquivalentTo(expected);
    }


    [Fact]
    public void MergeTypes()
    {
        var previous = new [] { new EntityAttributeMetaData("brightness", "Brightness", typeof(double)) };

        var current = new [] { new EntityAttributeMetaData("brightness", "Brightness", typeof(int)) };

        var result = TestAttributeMerge(previous, current);

        var expected = new [] {
            new EntityAttributeMetaData("brightness", "Brightness", typeof(object)),
        };

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void MergeWithBase()
    {
        var previous = new [] { new EntityAttributeMetaData("brightness", "Brightness", typeof(double)) };

        var current = new [] { new EntityAttributeMetaData("brightness", "Brightness", typeof(double)) };

        var result = TestAttributeMerge(previous, current, true);

        var expected = Array.Empty<EntityAttributeMetaData>();

        result.Should().BeEquivalentTo(expected);
    }



    [Fact]
    public void NullAndDoubleMergesToDouble()
    {
        var previous = new [] { new EntityAttributeMetaData("brightness", "Brightness", (Type?)null) };
        var current = new [] { new EntityAttributeMetaData("brightness", "Brightness", typeof(double)) };

        var result = TestAttributeMerge(previous, current);

        var expected = new [] { new EntityAttributeMetaData("brightness", "Brightness", typeof(double)) };

        result.Should().BeEquivalentTo(expected);

        // swap current and previous and merge again, should have same result
        result = TestAttributeMerge(current, previous);
        result.Should().BeEquivalentTo(expected);
    }


    private static IReadOnlyCollection<EntityAttributeMetaData> TestAttributeMerge(IReadOnlyList<EntityAttributeMetaData> previousAttr, IReadOnlyList<EntityAttributeMetaData> currentAttr, bool useBaseType = false)
    {
        var previous = new EntitiesMetaData{Domains = new []{new EntityDomainMetadata("light", false, Array.Empty<EntityMetaData>(),
            previousAttr)}};

        var current =  new EntitiesMetaData{Domains =new []{new EntityDomainMetadata("light", false, Array.Empty<EntityMetaData>(),
            currentAttr)}};

        return EntityMetaDataMerger.Merge(new CodeGenerationSettings(){UseAttributeBaseClasses = useBaseType}, previous, current).Domains.Single().Attributes;
    }
}
