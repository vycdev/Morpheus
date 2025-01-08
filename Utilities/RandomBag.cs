public class RandomBag
{
    private List<string> items;
    private List<string> currentBag;
    private Random random;

    public RandomBag(List<string> initialItems)
    {
        if (initialItems == null || initialItems.Count == 0)
            throw new ArgumentException("Initial list cannot be null or empty.");

        items = new List<string>(initialItems);
        currentBag = new List<string>(items);
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