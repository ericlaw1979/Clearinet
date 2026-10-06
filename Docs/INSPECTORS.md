An Inspector is used to display (and optionally tamper with) the content of a HTTP request or response message.

By default, your Inspector object should simply offer its name and respond to queries about how interested it is in
a given message. For instance an Image inspector will score responses with "Content-Type: image/jpeg" highly, while
responses of "Content-Type: text/html" will return a low score. The App will then attempt to select the most suitable
Inspector when the user hits Enter or double-clicks on an Exchange.

Your Inspector will be provided a tab into which its UI will be rendered. This is typically one of the tabs on the
Inspectors page in the main app UI, but it may be in the AutoResponder editor or in a standalone "Inspect in New Window" window.

For performance reasons, you should avoid doing anything expensive (e.g. loading dependent DLLs, creating heavy UI components) until
you've been told to do so with the `.AddToTab` call.

In many cases, your Inspector may be asked to inspect exchanges in rapid sequence; this could happen:

   - Intentionally: the user is scrolling through the list rapidly looking for e.g. a particular image, or
   - Unintentionally: the user is scrolling through the list and not even looking at the inspector at all.

Unfortunately, these scenarios are in conflict: we could "debounce" updates to prevent unwanted updates and allow the UI to be
more responsive, but doing so will cause problems for the user who is intentionally rapidly cycling between entries. Handling of
this situation will probably be decided for you at the app level; we'll only call Assign() on you when we want you to inspect the
Exchange as quickly as possible.