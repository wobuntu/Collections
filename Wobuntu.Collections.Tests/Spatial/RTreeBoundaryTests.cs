using Wobuntu.Collections.Spatial;


namespace Wobuntu.Collections.Tests.Spatial;

public class RTreeBoundaryTests
{
  // Note that RTreeBoundary is a specialized readonly variant of a rectangle and works different to it.
  // E.g. infinite values are forbidden.

  [Theory]
  [InlineData(10d, 15d, 25d)]
  [InlineData(-10d, 35d, 25d)]
  [InlineData(-100d, 75d, -25d)]
  public void Right_CalculatedCorrectly(double x, double width, double expected)
  {
    var boundary = new RTreeBoundary(x, 0, width, 10);
    Assert.Equal(expected, boundary.Right);
  }

  [Theory]
  [InlineData(10d, 15d, 17.5d)]
  [InlineData(-10d, 35d, 7.5d)]
  [InlineData(-100d, 75d, -62.5d)]
  public void CenterX_CalculatedCorrectly(double x, double width, double expected)
  {
    var boundary = new RTreeBoundary(x, 0, width, 10);
    Assert.Equal(expected, boundary.CenterX);
  }

  [Theory]
  [InlineData(10d, 15d, 25d)]
  [InlineData(-10d, 35d, 25d)]
  [InlineData(-100d, 75d, -25d)]
  public void Bottom_CalculatedCorrectly(double y, double height, double expected)
  {
    var boundary = new RTreeBoundary(0, y, 10, height);
    Assert.Equal(expected, boundary.Bottom);
  }

  [Theory]
  [InlineData(10d, 15d, 17.5d)]
  [InlineData(-10d, 35d, 7.5d)]
  [InlineData(-100d, 75d, -62.5d)]
  public void CenterY_CalculatedCorrectly(double y, double height, double expected)
  {
    var boundary = new RTreeBoundary(0, y, 10, height);
    Assert.Equal(expected, boundary.CenterY);
  }

  [Fact]
  public void Ctor_NegativeWidth_Throws() =>
    Assert.Throws<ArgumentException>(() => new RTreeBoundary(0, 0, -1, 1));

  [Fact]
  public void Ctor_NegativeHeight_Throws() =>
    Assert.Throws<ArgumentException>(() => new RTreeBoundary(0, 0, 1, -1));

  [Fact]
  public void Ctor_InfiniteX_Throws() =>
    Assert.Throws<ArgumentException>(() => new RTreeBoundary(double.PositiveInfinity, 0, 1, 1));

  [Fact]
  public void Ctor_InfiniteY_Throws() =>
    Assert.Throws<ArgumentException>(() => new RTreeBoundary(0, double.PositiveInfinity, 1, 1));

  [Fact]
  public void Ctor_InfiniteWidth_Throws() =>
    Assert.Throws<ArgumentException>(() => new RTreeBoundary(0, 0, double.PositiveInfinity, 1));

  [Fact]
  public void Ctor_InfiniteHeight_Throws() =>
    Assert.Throws<ArgumentException>(() => new RTreeBoundary(0, 0, 1, double.PositiveInfinity));

  [Fact]
  public void Intersects_TouchingRight_ReturnsTrue()
  {
    // Arrange
    var first = new RTreeBoundary(0, 0, 100, 100);
    var second = new RTreeBoundary(100, 0, 100, 100);

    // Act / Assert
    Assert.True(first.Intersects(second));
  }

  [Fact]
  public void Intersects_TouchingLeft_ReturnsTrue()
  {
    // Arrange
    var first = new RTreeBoundary(100, 0, 100, 100);
    var second = new RTreeBoundary(0, 0, 100, 100);

    // Act / Assert
    Assert.True(first.Intersects(second));
  }

  [Fact]
  public void Intersects_TouchingBottom_ReturnsTrue()
  {
    // Arrange
    var first = new RTreeBoundary(0, 0, 100, 100);
    var second = new RTreeBoundary(0, 100, 100, 100);

    // Act / Assert
    Assert.True(first.Intersects(second));
  }

  [Fact]
  public void Intersects_TouchingTop_ReturnsTrue()
  {
    // Arrange
    var first = new RTreeBoundary(0, 100, 100, 100);
    var second = new RTreeBoundary(0, 0, 100, 100);

    // Act / Assert
    Assert.True(first.Intersects(second));
  }

  [Fact]
  public void Intersects_ContainingBoundary_ReturnsTrue()
  {
    // Arrange
    var first = new RTreeBoundary(0, 0, 100, 100);
    var second = new RTreeBoundary(10, 10, 80, 80);

    // Act / Assert
    Assert.True(first.Intersects(second));
    Assert.True(second.Intersects(first));
  }

  [Fact]
  public void Intersects_OneSideEmpty_ReturnsFalse()
  {
    // Arrange
    var first = new RTreeBoundary(0, 0, 0, 0);
    var second = new RTreeBoundary(-10, -10, 20, 20);

    // Act / Assert
    Assert.False(first.Intersects(second));
    Assert.False(second.Intersects(first));
  }

  [Fact]
  public void Contains_OneSideEmpty_ReturnsFalse()
  {
    // Arrange
    var first = new RTreeBoundary(0, 0, 0, 0);
    var second = new RTreeBoundary(-10, -10, 20, 20);

    // Act / Assert
    Assert.False(first.Contains(second));
    Assert.False(second.Contains(first));
    Assert.False(first.Contains(0, 0)); // point at origin of empty boundary
  }

  [Fact]
  public void Contains_EdgePoints_ReturnsTrue()
  {
    // Arrange
    var boundary = new RTreeBoundary(0, 0, 100, 100);

    // Act / Assert
    Assert.True(boundary.Contains(0, 0));
    Assert.True(boundary.Contains(0, 100));
    Assert.True(boundary.Contains(100, 0));
    Assert.True(boundary.Contains(100, 100));
    Assert.True(boundary.Contains(50, 50));

    Assert.True(boundary.Contains(new RTreeBoundary(0, 0, 1, 1)));
    Assert.True(boundary.Contains(new RTreeBoundary(0, 99, 1, 1)));
    Assert.True(boundary.Contains(new RTreeBoundary(99, 0, 1, 1)));
    Assert.True(boundary.Contains(new RTreeBoundary(99, 99, 1, 1)));
    Assert.True(boundary.Contains(new RTreeBoundary(50, 50, 1, 1)));

    Assert.True(boundary.Contains(new RTreeBoundary(0, 0, 10, 10)));
    Assert.True(boundary.Contains(new RTreeBoundary(90, 0, 10, 10)));
    Assert.True(boundary.Contains(new RTreeBoundary(0, 90, 10, 10)));
    Assert.True(boundary.Contains(new RTreeBoundary(90, 90, 10, 10)));
    Assert.True(boundary.Contains(new RTreeBoundary(45, 45, 10, 10)));

    Assert.False(boundary.Contains(new RTreeBoundary(-1, 0, 10, 10)));
    Assert.False(boundary.Contains(new RTreeBoundary(0, -1, 10, 10)));

    Assert.False(boundary.Contains(new RTreeBoundary(91, 0, 10, 10)));
    Assert.False(boundary.Contains(new RTreeBoundary(90, -1, 10, 10)));

    Assert.False(boundary.Contains(new RTreeBoundary(-1, 90, 10, 10)));
    Assert.False(boundary.Contains(new RTreeBoundary(0, 91, 10, 10)));

    Assert.False(boundary.Contains(new RTreeBoundary(91, 90, 10, 10)));
    Assert.False(boundary.Contains(new RTreeBoundary(90, 91, 10, 10)));
  }

  [Theory]
  [InlineData(0d, 0d, 90d, 0d, 0d, 0d, 100d, 10d)]
  [InlineData(90d, 0d, 0d, 0d, 0d, 0d, 100d, 10d)]
  [InlineData(0d, 0d, 0d, 90d, 0d, 0d, 10d, 100d)]
  [InlineData(0d, 90d, 0d, 0d, 0d, 0d, 10d, 100d)]
  [InlineData(0d, 0d, 90d, 90d, 0d, 0d, 100d, 100d)]
  [InlineData(90d, 90d, 0d, 0d, 0d, 0d, 100d, 100d)]
  [InlineData(90d, 90d, 90d, 90d, 90d, 90d, 10d, 10d)]
  public void Union_ReturnsCorrectSize(
    double x1, double y1, double x2, double y2, double rx, double ry, double rw, double rh)
  {
    // Arrange
    var first = new RTreeBoundary(x1, y1, 10, 10);
    var second = new RTreeBoundary(x2, y2, 10, 10);

    // Act
    var result = first.Union(second);

    // Assert
    Assert.Equal(rx, result.X);
    Assert.Equal(ry, result.Y);
    Assert.Equal(rw, result.Width);
    Assert.Equal(rh, result.Height);
  }

  [Fact]
  public void Union_WithZeroWidthOrHeightBoundaries_ZeroSizedTreatedAsUnset()
  {
    // Arrange
    var first = new RTreeBoundary(100, 200, 300, 400);
    var second = new RTreeBoundary();

    // Act
    var result1 = first.Union(second);
    var result2 = second.Union(first);

    // Assert
    Assert.Equal(first, result1);
    Assert.Equal(first, result2);
  }

}