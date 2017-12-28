## Disqus to StaticMan Comment Processor

This .NET Core project will process a Disqus comment export in XML format and generate comment files (YML) using the [StaticMan](http://staticman.net) system.

Important Notes:
* I did have to do some manual preprocessing of my XML file due to Disqus having multiple Thread's for the same blog post.
* There seems to be some discrepancies in the format for the YML comments so, I'd recommend checking to see what format your site creates those files in and then altering the **Post.GetFileText** method to write the data out correctly.
* I started out handling things very simply and then ended up making actual classes to represent **threads** and **posts**.  This is why you see a Dictionary with Thread IDs and Names and a List of Thread objects.
* The way I am displaying comments and handling replies is that there can only be one level deep replies.  So, this program will take all children comments for any comment and flatten them so all replies are only one level deep.