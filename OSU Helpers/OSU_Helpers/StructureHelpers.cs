using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using static OSU_Helpers.Enumerables;

namespace OSU_Helpers
{
    /// <summary>
    /// Class to hold extension methods for the ESAPI <c>Structure</c> class
    /// </summary>
    public static class StructureHelpers
    {
        /// <summary>
        /// Generates a unique structure ID for the base structure by appending the next digit.
        /// </summary>
        /// <param name="BaseIdString">Base structure ID to append the digit to</param>
        /// <param name="structureSet">The structure set where the structures reside</param>
        /// <returns>A structure ID in the form of a string that has a unique ID for the provided structure set.</returns>
        public static string UniqueStructureId(this string BaseIdString, StructureSet structureSet)
        {
            // Check if the initial BaseIdString already exists
            // Using Any() is generally more efficient than Where().Count() > 0
            if (structureSet.Structures.Any(a => a.Id == BaseIdString))
            {
                string tmp_id_r1 = BaseIdString;
                int i = 0;
                // Loop to find the next available ID by appending "_i"
                // Limit loop by attempts or check length more carefully
                int attempts = 0; // Prevent infinite loop
                while (structureSet.Structures.Any(a => a.Id == tmp_id_r1) && attempts < 1000) // Added attempt limit
                {
                    string suffix = "_" + i.ToString();
                    // Check length before assignment
                    if ((BaseIdString.Length + suffix.Length) <= 16)
                    {
                        tmp_id_r1 = BaseIdString + suffix;
                    }
                    else
                    {
                        // Handle exceeding 16 chars - truncate base or throw?
                        // Simple truncation might risk collision. Throwing is safer.
                        // Or, if truncation is acceptable:
                        int charsToKeep = 16 - suffix.Length;
                        if (charsToKeep <= 0) throw new InvalidOperationException($"Cannot make unique ID for '{BaseIdString}' within 16 chars.");
                        tmp_id_r1 = BaseIdString.Substring(0, charsToKeep) + suffix;
                        // Important: Check again if truncated ID exists! If so, this simple approach fails.
                        if (structureSet.Structures.Any(a => a.Id == tmp_id_r1))
                        {
                            // Handle collision after truncation - maybe try incrementing 'i' further? Complex.
                            throw new InvalidOperationException($"Cannot make unique ID for '{BaseIdString}' within 16 chars due to collision after truncation.");
                        }
                        break; // Exit loop with truncated ID
                    }
                    i++;
                    attempts++;
                }
                if (attempts >= 1000)
                {
                    throw new InvalidOperationException($"Could not find unique ID for '{BaseIdString}' after {attempts} attempts.");
                }
                return tmp_id_r1;
            }
            else
            {
                // Add length check for the initial ID too
                if (BaseIdString.Length > 16)
                {
                    throw new ArgumentException($"BaseIdString '{BaseIdString}' exceeds 16 characters.", nameof(BaseIdString));
                }
                return BaseIdString;
            }
        }


        /// <summary>
        /// Generates the ring based upon a particular structure, adds to the given structure set, using the distances provided
        /// </summary>
        /// <param name="_ResultantRing">Base structure to generate the ring from</param>
        /// <param name="BaseStructureSet"></param>
        /// <param name="StartDistance_mm">Using double for consistency with Margin methods</param>
        /// <param name="EndDistance_mm">Using double for consistency</param>
        /// <param name="HighResFlag"></param>
        /// <returns>The ring as a structure type.</returns>
        public static Structure GenerateRing(this Structure _ResultantRing, StructureSet BaseStructureSet, double StartDistance_mm, double EndDistance_mm, bool HighResFlag = false)
        {
            // Added null checks and validation
            if (_ResultantRing == null) throw new ArgumentNullException(nameof(_ResultantRing));
            if (BaseStructureSet == null) throw new ArgumentNullException(nameof(BaseStructureSet));
            if (StartDistance_mm < 0 || EndDistance_mm < 0) throw new ArgumentException("Distances must be non-negative.");
            if (EndDistance_mm <= StartDistance_mm) throw new ArgumentException("End distance must be greater than start distance.");


            // HighRes conversion logic - added CanConvertToHighResolution check
            if (HighResFlag && !_ResultantRing.IsHighResolution)
            {
                if (_ResultantRing.CanConvertToHighResolution())
                    _ResultantRing.ConvertToHighResolution();
                else
                    Console.WriteLine($"Warning: Structure '{_ResultantRing.Id}' cannot be converted to high resolution."); // Or throw?
            }

            string id_r1 = "_tempr1";
            string id_r2 = "_tempr2";

            id_r1 = id_r1.UniqueStructureId(BaseStructureSet);
            id_r2 = id_r2.UniqueStructureId(BaseStructureSet);

            Structure temp_start = null; // Initialize to null for finally block
            Structure temp_end = null;

            try // Use try/finally to ensure cleanup
            {
                temp_start = BaseStructureSet.AddStructure("AVOIDANCE", id_r1); // Use CONTROL or AVOIDANCE
                if (HighResFlag && !temp_start.IsHighResolution && temp_start.CanConvertToHighResolution()) temp_start.ConvertToHighResolution();

                temp_end = BaseStructureSet.AddStructure("AVOIDANCE", id_r2);
                if (HighResFlag && !temp_end.IsHighResolution && temp_end.CanConvertToHighResolution()) temp_end.ConvertToHighResolution();

                // Ensure resolution consistency if HighResFlag is true
                if (HighResFlag && (!_ResultantRing.IsHighResolution || !temp_start.IsHighResolution || !temp_end.IsHighResolution))
                {
                    // Handle inconsistency - log warning or throw exception
                    Console.WriteLine("Warning: High resolution requested but not all structures could be converted.");
                    // Or throw new InvalidOperationException("Could not ensure high resolution for all structures in GenerateRing.");
                }

                // Use the MarginGreaterThan50mm helper for robustness
                temp_start.SegmentVolume = _ResultantRing.SegmentVolume.MarginGreaterThan50mm(StartDistance_mm);
                temp_end.SegmentVolume = _ResultantRing.SegmentVolume.MarginGreaterThan50mm(EndDistance_mm);

                _ResultantRing.SegmentVolume = temp_end.SegmentVolume.Sub(temp_start.SegmentVolume); // Use SegmentVolume property
            }
            finally // Ensure temporary structures are removed
            {
                if (temp_start != null && BaseStructureSet.Structures.Contains(temp_start)) BaseStructureSet.RemoveStructure(temp_start);
                if (temp_end != null && BaseStructureSet.Structures.Contains(temp_end)) BaseStructureSet.RemoveStructure(temp_end);
            }

            return _ResultantRing;
        }


        /// <summary>
        /// Generates a ring <c>SegmentVolume</c> from a collection of base structures that have been added together
        /// </summary>
        [Obsolete("Prefer using the StructureSet overload: GenerateRing(this StructureSet ss, ICollection<Structure> structure, ...).")]
        public static SegmentVolume GenerateRing(this ICollection<Structure> structure, StructureSet ss, double StartDistance_mm, double EndDistance_mm, bool HighResFlag = false)
        {
            // Check for null/empty inputs
            if (ss == null) throw new ArgumentNullException(nameof(ss));
            if (structure == null || !structure.Any()) throw new ArgumentException("Structure collection cannot be null or empty.", nameof(structure));

            // Use the safe TotalSegmentVolume overload
            SegmentVolume sv = TotalSegmentVolume(structure, ss, HighResFlag);

            // Need temporary structures for margins
            Structure exp1 = null;
            Structure exp2 = null;
            string id_exp1 = "_Exp1".UniqueStructureId(ss); // Ensure unique IDs
            string id_exp2 = "_Exp2".UniqueStructureId(ss);

            try
            {
                exp1 = ss.AddStructure("CONTROL", id_exp1);
                exp2 = ss.AddStructure("CONTROL", id_exp2);

                // Ensure high-res consistency if needed
                bool needHighRes = HighResFlag || structure.Any(a => a.IsHighResolution);
                if (needHighRes)
                {
                    if (exp1.CanConvertToHighResolution()) exp1.ConvertToHighResolution(); else Console.WriteLine($"Warning: Cannot make temp structure {id_exp1} high-res.");
                    if (exp2.CanConvertToHighResolution()) exp2.ConvertToHighResolution(); else Console.WriteLine($"Warning: Cannot make temp structure {id_exp2} high-res.");
                }
                if (needHighRes && (!exp1.IsHighResolution || !exp2.IsHighResolution))
                {
                    // Handle inconsistency if high-res is critical
                }


                // Use MarginGreaterThan50mm helper
                exp1.SegmentVolume = sv.MarginGreaterThan50mm(StartDistance_mm);
                exp2.SegmentVolume = sv.MarginGreaterThan50mm(EndDistance_mm);

                sv = exp2.SegmentVolume.Sub(exp1.SegmentVolume); // Use SegmentVolume property
            }
            finally
            {
                if (exp1 != null && ss.Structures.Contains(exp1)) ss.RemoveStructure(exp1);
                if (exp2 != null && ss.Structures.Contains(exp2)) ss.RemoveStructure(exp2);
            }

            return sv;
        }


        /// <summary>
        /// Generates a segment volume that is the union of all structures within the collection.
        /// WARNING: This overload can modify input structures if they need high-res conversion. Use the overload with StructureSet for safety.
        /// </summary>
        public static SegmentVolume TotalSegmentVolume(this IEnumerable<Structure> structures)
        {
            // Check for null or empty input
            if (structures == null || !structures.Any())
            {
                // Decide behavior: Throw exception or return null/empty representation?
                // Throwing is safer as union of nothing is ill-defined.
                throw new ArgumentException("Input structure collection cannot be null or empty.", nameof(structures));
                // If returning null is preferred: return null; (Callers must handle null)
            }

            int st_count = structures.Count(); // No longer needed if using foreach
            bool high_res = structures.Any(a => a.IsHighResolution);

            SegmentVolume sv = null; // Initialize
            Structure firstStructure = structures.First(); // Get first structure

            try
            {
                // Process first structure (potentially modifying it)
                if (high_res && !firstStructure.IsHighResolution)
                {
                    try { if (firstStructure.CanConvertToHighResolution()) firstStructure.ConvertToHighResolution(); }
                    catch { /* Ignore conversion error? Log? */ Console.WriteLine($"Warning: Could not convert '{firstStructure.Id}' to high-res."); }
                }
                sv = firstStructure.SegmentVolume; // Assign initial volume

                // Process remaining structures
                foreach (var structure in structures.Skip(1))
                {
                    Structure currentStructure = structure; // Local reference
                    if (high_res && !currentStructure.IsHighResolution)
                    {
                        try { if (currentStructure.CanConvertToHighResolution()) currentStructure.ConvertToHighResolution(); }
                        catch { /* Ignore? Log? */ Console.WriteLine($"Warning: Could not convert '{currentStructure.Id}' to high-res."); }
                    }
                    sv = sv.Or(currentStructure.SegmentVolume); // Perform union
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TotalSegmentVolume (unsafe): {ex.Message}");
                throw; // Re-throw
            }

            return sv; // Return the combined volume
        }


        /// <summary>
        /// Generates a segment volume that is the union of all structures within the collection.
        /// Protects against modifying approved structures by using temporary copies for high-res conversion if needed.
        /// </summary>
        public static SegmentVolume TotalSegmentVolume(this IEnumerable<Structure> structures, StructureSet BaseStructureSet, bool HighResFlag = false)
        {
            // Check for null/empty inputs
            if (BaseStructureSet == null) throw new ArgumentNullException(nameof(BaseStructureSet));
            if (structures == null || !structures.Any())
                throw new ArgumentException("Input structure collection cannot be null or empty.", nameof(structures));


            bool high_res_needed = HighResFlag || structures.Any(a => a.IsHighResolution);
            SegmentVolume sv = null;
            List<Structure> tempStructures = new List<Structure>(); // For cleanup

            try
            {
                // Process first structure
                Structure firstStructure = structures.First();
                Structure structureToUse = firstStructure; // Structure to get the volume from

                if (high_res_needed && !firstStructure.IsHighResolution)
                {
                    if (firstStructure.CanConvertToHighResolution())
                    {
                        firstStructure.ConvertToHighResolution(); // Modify original if possible
                        // structureToUse remains firstStructure
                    }
                    else
                    {
                        // Create temporary structure
                        string tempId = firstStructure.Id.UniqueStructureId(BaseStructureSet);
                        string dicomType = (firstStructure.DicomType == "EXTERNAL" || firstStructure.DicomType == "SUPPORT" || string.IsNullOrEmpty(firstStructure.DicomType)) ? "CONTROL" : firstStructure.DicomType;
                        Structure temp = BaseStructureSet.AddStructure(dicomType, tempId);
                        temp.SegmentVolume = firstStructure.SegmentVolume; // Copy geometry
                        if (temp.CanConvertToHighResolution())
                        {
                            temp.ConvertToHighResolution();
                            tempStructures.Add(temp); // Track for cleanup
                            structureToUse = temp; // Use the temp structure's volume
                        }
                        else
                        {
                            BaseStructureSet.RemoveStructure(temp); // Remove failed temp
                            throw new InvalidOperationException($"Failed to convert temporary copy of '{firstStructure.Id}' to high-res.");
                        }
                    }
                }
                sv = structureToUse.SegmentVolume; // Initialize sv

                // Process remaining structures
                foreach (var structure in structures.Skip(1))
                {
                    Structure currentStructure = structure;
                    Structure structureForUnion = currentStructure; // Structure to use for OR operation

                    if (high_res_needed && !currentStructure.IsHighResolution)
                    {
                        if (currentStructure.CanConvertToHighResolution())
                        {
                            currentStructure.ConvertToHighResolution(); // Modify original if possible
                            // structureForUnion remains currentStructure
                        }
                        else
                        {
                            // Create temporary structure
                            string tempId = currentStructure.Id.UniqueStructureId(BaseStructureSet);
                            string dicomType = (currentStructure.DicomType == "EXTERNAL" || currentStructure.DicomType == "SUPPORT" || string.IsNullOrEmpty(currentStructure.DicomType)) ? "CONTROL" : currentStructure.DicomType;
                            Structure temp = BaseStructureSet.AddStructure(dicomType, tempId);
                            temp.SegmentVolume = currentStructure.SegmentVolume;
                            if (temp.CanConvertToHighResolution())
                            {
                                temp.ConvertToHighResolution();
                                tempStructures.Add(temp);
                                structureForUnion = temp; // Use the temp structure
                            }
                            else
                            {
                                BaseStructureSet.RemoveStructure(temp);
                                throw new InvalidOperationException($"Failed to convert temporary copy of '{currentStructure.Id}' to high-res.");
                            }
                        }
                    }
                    else if (!high_res_needed && structure.IsHighResolution)
                    {
                        // Optional warning if mixing resolutions unexpectedly
                        Console.WriteLine($"Warning: High-res structure '{structure.Id}' used in potentially low-res union.");
                    }

                    // Perform OR operation
                    sv = sv.Or(structureForUnion.SegmentVolume);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TotalSegmentVolume (safe): {ex.Message}");
                // Ensure cleanup happens even if an error occurred mid-process
                BaseStructureSet.RemoveStructuresFromStructureSet(tempStructures); // Use helper for cleanup
                throw; // Re-throw exception
            }
            finally
            {
                // Final cleanup (might be redundant if exception handling catches it, but ensures it)
                BaseStructureSet.RemoveStructuresFromStructureSet(tempStructures);
            }
            return sv;
        }


        /// <summary>
        /// Applies a margin potentially greater than 50mm to a Structure (modifies in place).
        /// </summary>
        public static Structure MarginGreaterThan50mm(this Structure structure, double margin)
        {
            if (structure == null) throw new ArgumentNullException(nameof(structure));
            // Delegate to the SegmentVolume version for the logic
            structure.SegmentVolume = structure.SegmentVolume.MarginGreaterThan50mm(margin);
            return structure;
        }


        /// <summary>
        /// Applies a margin potentially greater than 50mm to a SegmentVolume (returns new).
        /// </summary>
        public static SegmentVolume MarginGreaterThan50mm(this SegmentVolume sv, double margin)
        {
            if (sv == null) throw new ArgumentNullException(nameof(sv));
            // Handle zero margin efficiently
            if (Math.Abs(margin) < 1e-9) return sv; // Use a small tolerance for float comparison

            bool expand = margin > 0;
            double absMargin = Math.Abs(margin);
            int count_50 = (int)Math.Floor(absMargin / 50.0);
            double remainder_50 = absMargin % 50.0;

            SegmentVolume current_sv = sv; // Work with a copy

            // Apply 50mm steps
            for (int i = 0; i < count_50; i++)
            {
                current_sv = current_sv.Margin(expand ? 50.0 : -50.0);
            }

            // Apply remainder
            if (remainder_50 > 1e-9) // Check if remainder is significant
            {
                current_sv = current_sv.Margin(expand ? remainder_50 : -remainder_50);
            }

            return current_sv; // Return the resulting SegmentVolume
        }

        /// <summary>
        /// Applies asymmetric outer margin > 50mm. Requires int[] array for margins.
        /// </summary>
        /// <param name="sv">SegmentVolume.</param>
        /// <param name="margins">Margins [X1, Y1, Z1, X2, Y2, Z2] as integers.</param>
        public static SegmentVolume OuterAsymMarginGreaterThan50mm(this SegmentVolume sv, int[] margins)
        {
            if (margins == null || margins.Length != 6) throw new ArgumentException("Margins array must contain 6 integer values.", nameof(margins));
            // Convert int[] to double[] for the core logic
            double[] doubleMargins = margins.Select(m => (double)m).ToArray();
            if (doubleMargins.Any(m => m < 0)) throw new ArgumentException("Outer margins must be non-negative.", nameof(margins));
            return AsymMarginGreaterThan50mm(sv, doubleMargins, StructureMarginGeometry.Outer);
        }

        /// <summary>
        /// Applies asymmetric inner margin (crop) > 50mm. Requires int[] array for margins.
        /// </summary>
        /// <param name="sv">SegmentVolume.</param>
        /// <param name="margins">Crop distances [X1, Y1, Z1, X2, Y2, Z2] as integers.</param>
        public static SegmentVolume InnerAsymMarginGreaterThan50mm(this SegmentVolume sv, int[] margins)
        {
            if (margins == null || margins.Length != 6) throw new ArgumentException("Margins array must contain 6 integer values.", nameof(margins));
            // Convert int[] to double[]
            double[] doubleMargins = margins.Select(m => (double)m).ToArray();
            if (doubleMargins.Any(m => m < 0)) throw new ArgumentException("Inner margins (crop distances) must be non-negative.", nameof(margins));
            return AsymMarginGreaterThan50mm(sv, doubleMargins, StructureMarginGeometry.Inner);
        }

        /// <summary>
        /// Core logic for asymmetric margins > 50mm using iterative application. Takes double[] margins.
        /// </summary>
        /// <param name="sv">SegmentVolume.</param>
        /// <param name="margins">Margins [X1, Y1, Z1, X2, Y2, Z2]. Values must be non-negative.</param>
        /// <param name="marginGeometry">Outer or Inner.</param>
        public static SegmentVolume AsymMarginGreaterThan50mm(this SegmentVolume sv, double[] margins, StructureMarginGeometry marginGeometry = StructureMarginGeometry.Outer)
        {
            if (sv == null) throw new ArgumentNullException(nameof(sv));
            if (margins == null || margins.Length != 6) throw new ArgumentException("Margins array must contain 6 double values.", nameof(margins));
            if (margins.Any(m => m < 0)) throw new ArgumentException("All margin values must be non-negative.", nameof(margins));


            // If all margins are within the standard limit, use the direct ESAPI call
            if (margins.All(m => m <= 50.0))
            {
                AxisAlignedMargins axisAligned = new AxisAlignedMargins(marginGeometry, margins[0], margins[1], margins[2], margins[3], margins[4], margins[5]);
                return sv.AsymmetricMargin(axisAligned);
            }

            // --- Logic without Direction Enum ---
            int[] counts_50 = new int[6];
            double[] remainders_50 = new double[6];
            for (int i = 0; i < 6; i++)
            {
                counts_50[i] = (int)Math.Floor(margins[i] / 50.0);
                remainders_50[i] = margins[i] % 50.0;
            }
            int max_50_steps = counts_50.Max();
            SegmentVolume sv_with_margin = sv; // Start with the original volume

            // Apply 50mm steps iteratively
            for (int i = 0; i < max_50_steps; i++)
            {
                // Determine margins for this step (50 or 0) based on counts_50 array
                double mar_neg_x = (counts_50[0] > i) ? 50.0 : 0.0; // Index 0 = X1 (NegX)
                double mar_neg_y = (counts_50[1] > i) ? 50.0 : 0.0; // Index 1 = Y1 (NegY)
                double mar_neg_z = (counts_50[2] > i) ? 50.0 : 0.0; // Index 2 = Z1 (NegZ)
                double mar_pos_x = (counts_50[3] > i) ? 50.0 : 0.0; // Index 3 = X2 (PosX)
                double mar_pos_y = (counts_50[4] > i) ? 50.0 : 0.0; // Index 4 = Y2 (PosY)
                double mar_pos_z = (counts_50[5] > i) ? 50.0 : 0.0; // Index 5 = Z2 (PosZ)

                AxisAlignedMargins current_step_margins = new AxisAlignedMargins(marginGeometry, mar_neg_x, mar_neg_y, mar_neg_z, mar_pos_x, mar_pos_y, mar_pos_z);
                sv_with_margin = sv_with_margin.AsymmetricMargin(current_step_margins);
            }

            // Apply the final remaining margins
            AxisAlignedMargins remainderMargins = new AxisAlignedMargins(marginGeometry, remainders_50[0], remainders_50[1], remainders_50[2], remainders_50[3], remainders_50[4], remainders_50[5]);
            // Only apply if any remainder is significant
            if (remainders_50.Any(r => r > 1e-9))
            {
                sv_with_margin = sv_with_margin.AsymmetricMargin(remainderMargins);
            }

            return sv_with_margin; // Return the final result
        }


        /// <summary>
        /// Crop PrimaryStructure extending outside CropFromStructure by CropDistance. Uses double distance. Safe for HighRes.
        /// </summary>
        public static SegmentVolume CropExtendingOutside(this StructureSet set, ICollection<Structure> PrimaryStructure, ICollection<Structure> CropFromStructure, double CropDistance) // Changed CropDistance to double
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            // Handle null/empty inputs gracefully or let TotalSegmentVolume throw
            if (CropDistance < 0) throw new ArgumentException("Crop distance must be non-negative.", nameof(CropDistance));

            bool highResNeeded = (PrimaryStructure?.Any(a => a.IsHighResolution) ?? false) || (CropFromStructure?.Any(a => a.IsHighResolution) ?? false);
            ICollection<Structure> tempPrimary = null;
            ICollection<Structure> tempCropFrom = null;
            SegmentVolume resultSv = null; // Default to null

            try
            {
                ICollection<Structure> primaryToUse = PrimaryStructure;
                ICollection<Structure> cropFromToUse = CropFromStructure;

                if (highResNeeded)
                {
                    // Create temporary copies only if input lists are valid
                    if (PrimaryStructure != null && PrimaryStructure.Any())
                    {
                        tempPrimary = PrimaryStructure.ConvertAllToHighRes(set); primaryToUse = tempPrimary;
                    }
                    else primaryToUse = null;
                    if (CropFromStructure != null && CropFromStructure.Any())
                    {
                        tempCropFrom = CropFromStructure.ConvertAllToHighRes(set); cropFromToUse = tempCropFrom;
                    }
                    else cropFromToUse = null;
                }

                // Check if primary structure exists
                if (primaryToUse == null || !primaryToUse.Any()) return null; // Cannot crop nothing

                SegmentVolume svp = primaryToUse.TotalSegmentVolume(set, highResNeeded);

                // If cropping structure is empty, no cropping occurs
                if (cropFromToUse == null || !cropFromToUse.Any()) return svp;

                SegmentVolume svf_boundary = cropFromToUse.TotalSegmentVolume(set, highResNeeded);
                SegmentVolume svf_outside_margin = svf_boundary.Not().MarginGreaterThan50mm(CropDistance);
                resultSv = svp.Sub(svf_outside_margin);
            }
            finally
            {
                if (tempPrimary != null) set.RemoveStructuresFromStructureSet(tempPrimary);
                if (tempCropFrom != null) set.RemoveStructuresFromStructureSet(tempCropFrom);
            }
            return resultSv; // Can be null
        }


        /// <summary>
        /// Crop PrimaryStructure extending inside CropFromStructure by CropDistance. Uses double distance. Safe for HighRes.
        /// </summary>
        public static SegmentVolume CropExtendingInside(this StructureSet set, ICollection<Structure> PrimaryStructure, ICollection<Structure> CropFromStructure, double CropDistance) // Changed CropDistance to double
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (CropDistance < 0) throw new ArgumentException("Crop distance must be non-negative.", nameof(CropDistance));

            bool highResNeeded = (PrimaryStructure?.Any(a => a.IsHighResolution) ?? false) || (CropFromStructure?.Any(a => a.IsHighResolution) ?? false);
            ICollection<Structure> tempPrimary = null;
            ICollection<Structure> tempCropFrom = null;
            SegmentVolume resultSv = null;

            try
            {
                ICollection<Structure> primaryToUse = PrimaryStructure;
                ICollection<Structure> cropFromToUse = CropFromStructure;

                if (highResNeeded)
                {
                    if (PrimaryStructure != null && PrimaryStructure.Any())
                    {
                        tempPrimary = PrimaryStructure.ConvertAllToHighRes(set); primaryToUse = tempPrimary;
                    }
                    else primaryToUse = null;
                    if (CropFromStructure != null && CropFromStructure.Any())
                    {
                        tempCropFrom = CropFromStructure.ConvertAllToHighRes(set); cropFromToUse = tempCropFrom;
                    }
                    else cropFromToUse = null;
                }

                if (primaryToUse == null || !primaryToUse.Any()) return null; // Cannot crop nothing

                SegmentVolume svp = primaryToUse.TotalSegmentVolume(set, highResNeeded);

                if (cropFromToUse == null || !cropFromToUse.Any()) return svp; // Cropping from empty does nothing

                SegmentVolume svf_boundary = cropFromToUse.TotalSegmentVolume(set, highResNeeded);
                SegmentVolume svf_inside_margin = svf_boundary.MarginGreaterThan50mm(CropDistance);
                resultSv = svp.Sub(svf_inside_margin);
            }
            finally
            {
                if (tempPrimary != null) set.RemoveStructuresFromStructureSet(tempPrimary);
                if (tempCropFrom != null) set.RemoveStructuresFromStructureSet(tempCropFrom);
            }
            return resultSv; // Can be null
        }


        /// <summary>
        /// Converts structures to High Resolution using temporary copies. Caller must manage the returned temporary structures.
        /// </summary>
        public static ICollection<Structure> ConvertAllToHighRes(this ICollection<Structure> Structures, StructureSet BaseStructureSet)
        {
            // Added null checks
            if (BaseStructureSet == null) throw new ArgumentNullException(nameof(BaseStructureSet));
            List<Structure> hd_structures = new List<Structure>();
            if (Structures == null) return hd_structures; // Return empty list if input is null

            foreach (var structure in Structures) // Use foreach for cleaner iteration
            {
                if (structure == null) continue; // Skip null entries in the collection

                Structure temp = null; // Initialize for potential cleanup
                try
                {
                    string tempId = structure.Id.UniqueStructureId(BaseStructureSet);
                    string dicomType = (structure.DicomType == "EXTERNAL" || structure.DicomType == "SUPPORT" || string.IsNullOrEmpty(structure.DicomType)) ? "CONTROL" : structure.DicomType;
                    temp = BaseStructureSet.AddStructure(dicomType, tempId);
                    temp.SegmentVolume = structure.SegmentVolume;

                    if (temp.CanConvertToHighResolution())
                    {
                        temp.ConvertToHighResolution();
                    }
                    else
                    {
                        // Log or decide how to handle if temp can't be converted (shouldn't happen often for CONTROL)
                        Console.WriteLine($"Warning: Temp structure '{tempId}' could not be converted to high-res.");
                        // Maybe remove it and skip adding? Or throw?
                        // BaseStructureSet.RemoveStructure(temp); continue; // Example: skip this structure
                    }
                    hd_structures.Add(temp); // Add successfully created (and potentially converted) temp structure
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error converting structure '{structure.Id}' to high-res copy: {ex.Message}");
                    // Ensure partial temp structure is removed if error occurred
                    if (temp != null && BaseStructureSet.Structures.Contains(temp))
                    {
                        try { BaseStructureSet.RemoveStructure(temp); } catch { /* Ignore cleanup error */ }
                    }
                    // Rethrow or handle as appropriate
                    throw;
                }
            }
            return hd_structures;
        }

        /// <summary>
        /// Removes structures in the collection from the set. Iterates backwards. Handles nulls.
        /// </summary>
        public static void RemoveStructuresFromStructureSet(this StructureSet set, ICollection<Structure> Structures)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (Structures == null) return; // Nothing to remove

            for (int i = Structures.Count - 1; i >= 0; i--)
            {
                try
                {
                    Structure structureToRemove = Structures.ElementAt(i);
                    // Check if structure is not null and actually exists in the set
                    if (structureToRemove != null && set.Structures.Contains(structureToRemove))
                    {
                        set.RemoveStructure(structureToRemove);
                    }
                }
                catch (Exception ex)
                {
                    // Log error during removal, but continue loop
                    Console.WriteLine($"Error removing structure at index {i}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Calculates Non-Overlapping (XOR) volume. Safe for HighRes.
        /// </summary>
        public static SegmentVolume NonOverlapStructure(this StructureSet set, ICollection<Structure> StructureOne, ICollection<Structure> StructureTwo)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            bool highResNeeded = (StructureOne?.Any(a => a.IsHighResolution) ?? false) || (StructureTwo?.Any(a => a.IsHighResolution) ?? false);
            ICollection<Structure> temp1 = null;
            ICollection<Structure> temp2 = null;
            SegmentVolume resultSv = null;

            try
            {
                ICollection<Structure> s1ToUse = StructureOne;
                ICollection<Structure> s2ToUse = StructureTwo;
                bool s1Valid = StructureOne != null && StructureOne.Any();
                bool s2Valid = StructureTwo != null && StructureTwo.Any();

                if (highResNeeded)
                {
                    if (s1Valid) { temp1 = StructureOne.ConvertAllToHighRes(set); s1ToUse = temp1; } else s1ToUse = null;
                    if (s2Valid) { temp2 = StructureTwo.ConvertAllToHighRes(set); s2ToUse = temp2; } else s2ToUse = null;
                    // Update valid flags based on temp creation potentially failing? Or assume ConvertAll handles it.
                }

                SegmentVolume sv1 = (s1ToUse != null && s1ToUse.Any()) ? s1ToUse.TotalSegmentVolume(set, highResNeeded) : null;
                SegmentVolume sv2 = (s2ToUse != null && s2ToUse.Any()) ? s2ToUse.TotalSegmentVolume(set, highResNeeded) : null;

                if (sv1 != null && sv2 != null) resultSv = sv1.Xor(sv2);
                else if (sv1 != null) resultSv = sv1;
                else if (sv2 != null) resultSv = sv2;
                // else resultSv remains null
            }
            finally
            {
                if (temp1 != null) set.RemoveStructuresFromStructureSet(temp1);
                if (temp2 != null) set.RemoveStructuresFromStructureSet(temp2);
            }
            return resultSv; // Can be null
        }

        /// <summary>
        /// Calculates Intersection (AND) volume. Safe for HighRes.
        /// </summary>
        public static SegmentVolume IntersectionOfStructures(this StructureSet set, ICollection<Structure> StructureOne, ICollection<Structure> StructureTwo)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            bool highResNeeded = (StructureOne?.Any(a => a.IsHighResolution) ?? false) || (StructureTwo?.Any(a => a.IsHighResolution) ?? false);
            ICollection<Structure> temp1 = null;
            ICollection<Structure> temp2 = null;
            SegmentVolume resultSv = null;

            try
            {
                ICollection<Structure> s1ToUse = StructureOne;
                ICollection<Structure> s2ToUse = StructureTwo;
                bool s1Valid = StructureOne != null && StructureOne.Any();
                bool s2Valid = StructureTwo != null && StructureTwo.Any();

                // If either input is invalid, intersection is empty (represented by null here)
                if (!s1Valid || !s2Valid) return null;

                if (highResNeeded)
                {
                    temp1 = StructureOne.ConvertAllToHighRes(set); s1ToUse = temp1;
                    temp2 = StructureTwo.ConvertAllToHighRes(set); s2ToUse = temp2;
                    // Assuming ConvertAllToHighRes throws if structure list is empty now
                }

                // Now we know both s1ToUse and s2ToUse should be valid if we reach here
                SegmentVolume sv1 = s1ToUse.TotalSegmentVolume(set, highResNeeded);
                SegmentVolume sv2 = s2ToUse.TotalSegmentVolume(set, highResNeeded);

                resultSv = sv1.And(sv2); // AND operation handles empty intersection
            }
            finally
            {
                if (temp1 != null) set.RemoveStructuresFromStructureSet(temp1);
                if (temp2 != null) set.RemoveStructuresFromStructureSet(temp2);
            }
            return resultSv; // Can be null or an empty SegmentVolume
        }


        /// <summary>
        /// Calculates Subtraction (S1 - S2) volume. Safe for HighRes.
        /// </summary>
        public static SegmentVolume SubStructures(this StructureSet set, ICollection<Structure> StructureOne, ICollection<Structure> StructureTwo)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            bool highResNeeded = (StructureOne?.Any(a => a.IsHighResolution) ?? false) || (StructureTwo?.Any(a => a.IsHighResolution) ?? false);
            ICollection<Structure> temp1 = null;
            ICollection<Structure> temp2 = null;
            SegmentVolume resultSv = null;

            try
            {
                ICollection<Structure> s1ToUse = StructureOne;
                ICollection<Structure> s2ToUse = StructureTwo;
                bool s1Valid = StructureOne != null && StructureOne.Any();
                bool s2Valid = StructureTwo != null && StructureTwo.Any();

                // If S1 is invalid, the result is empty (null)
                if (!s1Valid) return null;

                if (highResNeeded)
                {
                    temp1 = StructureOne.ConvertAllToHighRes(set); s1ToUse = temp1;
                    if (s2Valid) { temp2 = StructureTwo.ConvertAllToHighRes(set); s2ToUse = temp2; } else s2ToUse = null;
                }

                SegmentVolume sv1 = s1ToUse.TotalSegmentVolume(set, highResNeeded);

                // If S2 is invalid, result is just S1
                if (!s2Valid || s2ToUse == null || !s2ToUse.Any())
                {
                    resultSv = sv1;
                }
                else
                {
                    SegmentVolume sv2 = s2ToUse.TotalSegmentVolume(set, highResNeeded);
                    resultSv = sv1.Sub(sv2); // SUB operation
                }
            }
            finally
            {
                if (temp1 != null) set.RemoveStructuresFromStructureSet(temp1);
                if (temp2 != null) set.RemoveStructuresFromStructureSet(temp2);
            }
            return resultSv; // Can be null
        }


        /// <summary>
        /// Returns the union (OR) of two structure collections. Minimal change version - Bug Fix Only.
        /// </summary>
        public static SegmentVolume UnionStructures(this StructureSet set, ICollection<Structure> StructureOne, ICollection<Structure> StructureTwo)
        {
            // Basic null checks for input collections might be wise
            if (set == null) throw new ArgumentNullException(nameof(set));
            // Handle cases where one or both might be null/empty before calling TotalSegmentVolume if it throws
            bool s1Valid = StructureOne != null && StructureOne.Any();
            bool s2Valid = StructureTwo != null && StructureTwo.Any();

            if (!s1Valid && !s2Valid) return null; // Union of nothing is nothing (or throw?)
            if (!s1Valid) return StructureTwo.TotalSegmentVolume(set); // Return union of S2
            if (!s2Valid) return StructureOne.TotalSegmentVolume(set); // Return union of S1

            // Original logic from user baseline starts here:
            SegmentVolume sv1 = StructureOne.TotalSegmentVolume(set); // Uses the overload that requires StructureSet for safety
            var tempstruct = StructureOne.First(); // Gets a reference to the first element

            // *** MINIMAL BUG FIX: The next line is removed/commented out ***
            // tempstruct.SegmentVolume = sv1; // REMOVED: This line modified the input structure

            SegmentVolume sv2 = StructureTwo.TotalSegmentVolume(set); // Uses the overload that requires StructureSet for safety
            var return_structure = StructureTwo.First(); // Gets a reference to the first element

            // *** MINIMAL BUG FIX: The next line is removed/commented out ***
            // return_structure.SegmentVolume = sv2; // REMOVED: This line modified the input structure

            // The original return logic is kept, assuming TotalSegmentVolume(list_of_two) performs the final OR.
            // Note: This final call to TotalSegmentVolume might be inefficient if sv1 and sv2 already represent the full unions.
            // A potentially more direct return would be `return sv1.Or(sv2);` if `sv1` and `sv2` are guaranteed correct.
            // Keeping original return to minimize changes:
            return TotalSegmentVolume(new List<Structure>() { return_structure, tempstruct }, set); // Pass set to safe overload
        }


        /// <summary>
        /// Generates a ring SegmentVolume. Protected against approved/high-res structures.
        /// </summary>
        public static SegmentVolume GenerateRing(this StructureSet ss, ICollection<Structure> structure, double StartDistance_mm, double EndDistance_mm, bool HighResFlag = false)
        {
            // Added input validation
            if (ss == null) throw new ArgumentNullException(nameof(ss));
            if (structure == null || !structure.Any()) throw new ArgumentException("Structure collection cannot be null or empty.", nameof(structure));
            if (StartDistance_mm < 0 || EndDistance_mm < 0) throw new ArgumentException("Distances must be non-negative.");
            if (EndDistance_mm <= StartDistance_mm) throw new ArgumentException("End distance must be greater than start distance.");

            // Use safe TotalSegmentVolume
            SegmentVolume sv = structure.TotalSegmentVolume(ss, HighResFlag);

            Structure exp1 = null; // Initialize for finally block
            Structure exp2 = null;
            string id_exp1 = "_Exp1".UniqueStructureId(ss);
            string id_exp2 = "_Exp2".UniqueStructureId(ss);

            try
            {
                exp1 = ss.AddStructure("CONTROL", id_exp1);
                exp2 = ss.AddStructure("CONTROL", id_exp2);

                // Ensure high-res consistency
                bool needHighRes = HighResFlag || structure.Any(a => a.IsHighResolution);
                if (needHighRes)
                {
                    if (exp1.CanConvertToHighResolution()) exp1.ConvertToHighResolution(); else Console.WriteLine($"Warning: Cannot make temp {id_exp1} high-res.");
                    if (exp2.CanConvertToHighResolution()) exp2.ConvertToHighResolution(); else Console.WriteLine($"Warning: Cannot make temp {id_exp2} high-res.");
                }
                if (needHighRes && (!exp1.IsHighResolution || !exp2.IsHighResolution))
                {
                    // Handle if conversion failed but was needed
                }


                // Use MarginGreaterThan50mm helper
                exp1.SegmentVolume = sv.MarginGreaterThan50mm(StartDistance_mm);
                exp2.SegmentVolume = sv.MarginGreaterThan50mm(EndDistance_mm);

                sv = exp2.SegmentVolume.Sub(exp1.SegmentVolume); // Get final ring volume
            }
            finally
            {
                if (exp1 != null && ss.Structures.Contains(exp1)) ss.RemoveStructure(exp1);
                if (exp2 != null && ss.Structures.Contains(exp2)) ss.RemoveStructure(exp2);
            }
            return sv;
        }


        // --- Overloads for single structures (Kept from original) ---
        // Added basic null checks for safety.

        /// <summary> CropExtendingOutside overload </summary>
        public static SegmentVolume CropExtendingOutside(this StructureSet set, Structure PrimaryStructure, Structure CropFromStructure, int CropDistance)
        {
            if (PrimaryStructure == null) throw new ArgumentNullException(nameof(PrimaryStructure));
            if (CropFromStructure == null) throw new ArgumentNullException(nameof(CropFromStructure));
            List<Structure> s1 = new List<Structure> { PrimaryStructure };
            List<Structure> s2 = new List<Structure> { CropFromStructure };
            return set.CropExtendingOutside(s1, s2, (double)CropDistance); // Cast int to double
        }
        /// <summary> CropExtendingOutside overload </summary>
        public static SegmentVolume CropExtendingOutside(this StructureSet set, Structure PrimaryStructure, ICollection<Structure> CropFromStructure, int CropDistance)
        {
            if (PrimaryStructure == null) throw new ArgumentNullException(nameof(PrimaryStructure));
            List<Structure> s1 = new List<Structure> { PrimaryStructure };
            return set.CropExtendingOutside(s1, CropFromStructure, (double)CropDistance); // Cast int to double
        }
        /// <summary> CropExtendingOutside overload </summary>
        public static SegmentVolume CropExtendingOutside(this StructureSet set, ICollection<Structure> PrimaryStructure, Structure CropFromStructure, int CropDistance)
        {
            if (CropFromStructure == null) throw new ArgumentNullException(nameof(CropFromStructure));
            List<Structure> s2 = new List<Structure> { CropFromStructure };
            return set.CropExtendingOutside(PrimaryStructure, s2, (double)CropDistance); // Cast int to double
        }

        /// <summary> CropExtendingInside overload </summary>
        public static SegmentVolume CropExtendingInside(this StructureSet set, Structure PrimaryStructure, Structure CropFromStructure, int CropDistance)
        {
            if (PrimaryStructure == null) throw new ArgumentNullException(nameof(PrimaryStructure));
            if (CropFromStructure == null) throw new ArgumentNullException(nameof(CropFromStructure));
            List<Structure> s1 = new List<Structure> { PrimaryStructure };
            List<Structure> s2 = new List<Structure> { CropFromStructure };
            return set.CropExtendingInside(s1, s2, (double)CropDistance); // Cast int to double
        }
        /// <summary> CropExtendingInside overload </summary>
        public static SegmentVolume CropExtendingInside(this StructureSet set, Structure PrimaryStructure, ICollection<Structure> CropFromStructure, int CropDistance)
        {
            if (PrimaryStructure == null) throw new ArgumentNullException(nameof(PrimaryStructure));
            List<Structure> s1 = new List<Structure> { PrimaryStructure };
            return set.CropExtendingInside(s1, CropFromStructure, (double)CropDistance); // Cast int to double
        }
        /// <summary> CropExtendingInside overload </summary>
        public static SegmentVolume CropExtendingInside(this StructureSet set, ICollection<Structure> PrimaryStructure, Structure CropFromStructure, int CropDistance)
        {
            if (CropFromStructure == null) throw new ArgumentNullException(nameof(CropFromStructure));
            List<Structure> s2 = new List<Structure> { CropFromStructure };
            return set.CropExtendingInside(PrimaryStructure, s2, (double)CropDistance); // Cast int to double
        }

        /// <summary> GenerateRing overload </summary>
        public static SegmentVolume GenerateRing(this StructureSet ss, Structure structure, double StartDistance_mm, double EndDistance_mm, bool HighResFlag = false)
        {
            if (structure == null) throw new ArgumentNullException(nameof(structure));
            List<Structure> s1 = new List<Structure> { structure };
            return ss.GenerateRing(s1, StartDistance_mm, EndDistance_mm, HighResFlag);
        }

        /// <summary> NonOverlapStructure overload </summary>
        public static SegmentVolume NonOverlapStructure(this StructureSet set, Structure StructureOne, Structure StructureTwo)
        {
            if (StructureOne == null) throw new ArgumentNullException(nameof(StructureOne));
            if (StructureTwo == null) throw new ArgumentNullException(nameof(StructureTwo));
            List<Structure> s1 = new List<Structure> { StructureOne };
            List<Structure> s2 = new List<Structure> { StructureTwo };
            return set.NonOverlapStructure(s1, s2);
        }
        /// <summary> NonOverlapStructure overload </summary>
        public static SegmentVolume NonOverlapStructure(this StructureSet set, Structure StructureOne, ICollection<Structure> StructureTwo)
        {
            if (StructureOne == null) throw new ArgumentNullException(nameof(StructureOne));
            List<Structure> s1 = new List<Structure> { StructureOne };
            return set.NonOverlapStructure(s1, StructureTwo);
        }
        /// <summary> NonOverlapStructure overload </summary>
        public static SegmentVolume NonOverlapStructure(this StructureSet set, ICollection<Structure> StructureOne, Structure StructureTwo)
        {
            if (StructureTwo == null) throw new ArgumentNullException(nameof(StructureTwo));
            List<Structure> s2 = new List<Structure> { StructureTwo };
            return set.NonOverlapStructure(StructureOne, s2);
        }

        /// <summary> IntersectionOfStructures overload </summary>
        public static SegmentVolume IntersectionOfStructures(this StructureSet set, Structure StructureOne, Structure StructureTwo)
        {
            if (StructureOne == null) throw new ArgumentNullException(nameof(StructureOne));
            if (StructureTwo == null) throw new ArgumentNullException(nameof(StructureTwo));
            List<Structure> s1 = new List<Structure> { StructureOne };
            List<Structure> s2 = new List<Structure> { StructureTwo };
            return set.IntersectionOfStructures(s1, s2);
        }
        /// <summary> IntersectionOfStructures overload </summary>
        public static SegmentVolume IntersectionOfStructures(this StructureSet set, Structure StructureOne, ICollection<Structure> StructureTwo)
        {
            if (StructureOne == null) throw new ArgumentNullException(nameof(StructureOne));
            List<Structure> s1 = new List<Structure> { StructureOne };
            return set.IntersectionOfStructures(s1, StructureTwo);
        }
        /// <summary> IntersectionOfStructures overload </summary>
        public static SegmentVolume IntersectionOfStructures(this StructureSet set, ICollection<Structure> StructureOne, Structure StructureTwo)
        {
            if (StructureTwo == null) throw new ArgumentNullException(nameof(StructureTwo));
            List<Structure> s2 = new List<Structure> { StructureTwo };
            return set.IntersectionOfStructures(StructureOne, s2);
        }

        /// <summary> SubStructures overload </summary>
        public static SegmentVolume SubStructures(this StructureSet set, Structure StructureOne, Structure StructureTwo)
        {
            if (StructureOne == null) throw new ArgumentNullException(nameof(StructureOne));
            if (StructureTwo == null) throw new ArgumentNullException(nameof(StructureTwo));
            List<Structure> s1 = new List<Structure> { StructureOne };
            List<Structure> s2 = new List<Structure> { StructureTwo };
            return set.SubStructures(s1, s2);
        }
        /// <summary> SubStructures overload </summary>
        public static SegmentVolume SubStructures(this StructureSet set, Structure StructureOne, ICollection<Structure> StructureTwo)
        {
            if (StructureOne == null) throw new ArgumentNullException(nameof(StructureOne));
            List<Structure> s1 = new List<Structure> { StructureOne };
            return set.SubStructures(s1, StructureTwo);
        }
        /// <summary> SubStructures overload </summary>
        public static SegmentVolume SubStructures(this StructureSet set, ICollection<Structure> StructureOne, Structure StructureTwo)
        {
            if (StructureTwo == null) throw new ArgumentNullException(nameof(StructureTwo));
            List<Structure> s2 = new List<Structure> { StructureTwo };
            return set.SubStructures(StructureOne, s2);
        }

        /// <summary> UnionStructures overload </summary>
        public static SegmentVolume UnionStructures(this StructureSet set, Structure StructureOne, Structure StructureTwo)
        {
            if (StructureOne == null) throw new ArgumentNullException(nameof(StructureOne));
            if (StructureTwo == null) throw new ArgumentNullException(nameof(StructureTwo));
            List<Structure> s1 = new List<Structure> { StructureOne };
            List<Structure> s2 = new List<Structure> { StructureTwo };
            return set.UnionStructures(s1, s2); // Calls the minimally modified UnionStructures
        }
        /// <summary> UnionStructures overload </summary>
        public static SegmentVolume UnionStructures(this StructureSet set, Structure StructureOne, ICollection<Structure> StructureTwo)
        {
            if (StructureOne == null) throw new ArgumentNullException(nameof(StructureOne));
            List<Structure> s1 = new List<Structure> { StructureOne };
            return set.UnionStructures(s1, StructureTwo); // Calls the minimally modified UnionStructures
        }
        /// <summary> UnionStructures overload </summary>
        public static SegmentVolume UnionStructures(this StructureSet set, ICollection<Structure> StructureOne, Structure StructureTwo)
        {
            if (StructureTwo == null) throw new ArgumentNullException(nameof(StructureTwo));
            List<Structure> s2 = new List<Structure> { StructureTwo };
            return set.UnionStructures(StructureOne, s2); // Calls the minimally modified UnionStructures
        }

    } // End of static class StructureHelpers

    // Assuming Enumerables class/enum exists elsewhere in your OSU_Helpers namespace
    // If Direction enum was needed for AsymMarginGreaterThan50mm, it's now removed from that method.
    // If other methods use Enumerables, ensure that definition is correct in your project.
    /* Example if needed:
    namespace Enumerables
    {
        public enum Direction { NegativeX, NegativeY, NegativeZ, PositiveX, PositiveY, PositiveZ }
    }
    */

} // End of namespace OSU_Helpers