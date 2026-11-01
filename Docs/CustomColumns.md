(This documentation page is referenced directly by the "Add Columns..." dialog box)

The Exchanges List at the left side of the user interface provides a list of captured (or loaded) [Web Exchanges](EXCHANGES.md). By default, the columns of this list show the most useful attributes of the Exchange, including the URL, request method, response size and content-type and so on. However, depending on your needs, you may find it useful to add new columns.

To do so, you can:

- Add a new column by right-clicking the column headers and choosing `Add Columns...` and then choosing what data to show in your new column. Currently, you can add columns for Request Headers, Response Headers, or Flags on the Exchange. In the future, Timers and "Calculated" columns will be available.
- Bind a script function to a new column; click Rules > Customize Rules and look for the `BindUIColumn` attribute to find a sample of doing this.
