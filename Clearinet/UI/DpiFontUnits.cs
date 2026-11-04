namespace Clearinet
{
    /// <summary>
    /// Converts logical font points to and from Clearinet's DPI-adjusted managed-font convention.
    /// These conversions do not create fonts or handles, change layout, or convert RichEdit twips.
    /// </summary>
    public static class DpiFontUnits
    {
        /// <summary>
        /// Converts logical configured points to DPI-adjusted managed font points.
        /// The caller supplies the target control's established DPI, not the primary display's DPI.
        /// This convention is not appropriate for every native or GDI+ font consumer.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">The supplied DPI is not positive.</exception>
        public static float LogicalFontPointsToManagedFontPoints(float points, int deviceDpi)
        {
            if (deviceDpi <= 0) throw new System.ArgumentOutOfRangeException(nameof(deviceDpi));
            return points * deviceDpi / 96f;
        }

        /// <summary>
        /// Recovers logical points from a value following Clearinet's DPI-adjusted managed-font convention.
        /// Do not use this inverse on an already-logical point size.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">The supplied DPI is not positive.</exception>
        public static float ManagedFontPointsToLogicalFontPoints(float points, int deviceDpi)
        {
            if (deviceDpi <= 0) throw new System.ArgumentOutOfRangeException(nameof(deviceDpi));
            return points * 96f / deviceDpi;
        }
    }
}
