public class RandomBag
{
    private readonly List<string> items;
    private readonly List<string> currentBag;
    private readonly Random random;

    public RandomBag(List<string> initialItems)
    {
        if (initialItems == null || initialItems.Count == 0)
            throw new ArgumentException("Initial list cannot be null or empty.");

        items = [.. initialItems];
        currentBag = [.. items];
        random = new Random();
    }

    public string Random()
    {
        if (currentBag.Count == 0)
        {
            // Refill the bag with all items when the current bag is empty
            currentBag.AddRange(items);
        }

        // Pick a random index from the current bag
        int index = random.Next(currentBag.Count);

        // Remove the selected item from the bag and return it
        string selectedItem = currentBag[index];
        currentBag.RemoveAt(index);

        return selectedItem;
    }
}
