# Improved pdf_accessories_extractor - Ori.py to include Category extraction based on page text context
# Includes fixes for carrying forward previous category, filtering output categories, and fixing date format.

import pdfplumber
import sys
import re
import logging
from pathlib import Path
from typing import List, Dict, Optional, Tuple

# ==========================================
# CONFIGURATION
# ==========================================

# Client name noise removal
EXCLUDED_WORDS = ["ok", "acc", "kit", "logos", "conforme", "pneus","att.","att. RV", "66", "relavage","tapis"]
CLIENT_CLEAN_PATTERN = re.compile(
    r'\b(' + '|'.join(map(re.escape, EXCLUDED_WORDS)) + r')\b',
    re.IGNORECASE
)

# Paths
ROOT_DIR = Path(__file__).parent.absolute()
OUTPUT_FILE = ROOT_DIR / "output.txt"
LOG_FILE = ROOT_DIR / "extraction_debug.log"

# Fixed FRENCH_MONTHS: Keys without trailing space, Values without trailing space
FRENCH_MONTHS = {
    "janvier": "01", "février": "02", "mars": "03", "avril": "04",
    "mai": "05", "juin": "06", "juillet": "07", "août": "08",
    "septembre": "09", "octobre": "10", "novembre": "11", "décembre": "12"
}

# Define possible categories for internal detection
CATEGORIES = [
    "ACCESSOIRE CITROEN",
    "ACCESSOIRE PEUGEOT",
    "INTERVENANT EXT PEUGEOT",
    "TRANSPORT CITROEN I",
    "TRANSPORT CITROEN II",
    "TRANSPORT OPEL",
    "TRANSPORT PEUGEOT I",
    "TRANSPORT PEUGEOT II",
]

# Define categories to be included in the final output
OUTPUT_CATEGORIES = [
    "ACCESSOIRE CITROEN",
    "ACCESSOIRE PEUGEOT",
]

# Precompile regex for performance
CATEGORY_REGEX = re.compile('|'.join(re.escape(cat) for cat in CATEGORIES), re.IGNORECASE)


class VINExtractor:
    def __init__(self, pdf_folder: Path):
        self.pdf_folder = pdf_folder
        self._setup_logging()

    def _setup_logging(self):
        """Configure logging to file and console"""
        self.logger = logging.getLogger("VINExtractor")
        self.logger.setLevel(logging.DEBUG)
        self.logger.handlers = []

        # File handler - detailed logs
        fh = logging.FileHandler(LOG_FILE, mode='w', encoding='utf-8')
        fh.setFormatter(logging.Formatter('%(asctime)s [%(levelname)s] %(message)s'))
        fh.setLevel(logging.DEBUG)

        # Console handler - summary only
        ch = logging.StreamHandler()
        ch.setFormatter(logging.Formatter('%(message)s'))
        ch.setLevel(logging.INFO)

        self.logger.addHandler(fh)
        self.logger.addHandler(ch)

    def run(self):
        """Main extraction process"""
        self.logger.info(f"🚀 Starting extraction from: {self.pdf_folder}")

        pdf_files = sorted(self.pdf_folder.glob("*.pdf"))

        if not pdf_files:
            self.logger.error("❌ No PDF files found!")
            return

        self.logger.info(f"📁 Found {len(pdf_files)} PDF file(s)\n")

        try:
            with open(OUTPUT_FILE, 'w', encoding='utf-8') as f_out:
                total_vins = 0

                for pdf_file in pdf_files:
                    # Reset the state variable for each new PDF file
                    self.previous_category = "Unknown" # Initialize for the first page of the file
                    vins = self._process_pdf(pdf_file, f_out)
                    total_vins += vins
                    self.logger.info(f"   ✓ {pdf_file.name}: {vins} VINs ")

                self.logger.info(f"\n{'='*60}")
                self.logger.info(f"✅ COMPLETE: {total_vins} total VINs extracted")
                self.logger.info(f"📄 Output: {OUTPUT_FILE.name}")
                self.logger.info(f"📋 Debug log: {LOG_FILE.name}")
                self.logger.info(f"{'='*60}")

        except Exception as e:
            self.logger.critical(f"❌ FATAL ERROR: {e}", exc_info=True)

    def _process_pdf(self, pdf_path: Path, f_out) -> int:
        """Extract all VINs from a single PDF"""
        vins_extracted = 0

        try:
            with pdfplumber.open(pdf_path) as pdf:
                # Extract date from first page
                date = self._extract_date(pdf.pages[0])

                self.logger.debug(f"\n{'='*60}")
                self.logger.debug(f"Processing: {pdf_path.name}")
                self.logger.debug(f"Date: {date}")
                self.logger.debug(f"{'='*60}")

                # Process each page
                for page_num, page in enumerate(pdf.pages, 1):
                    
                    # --- CRITICAL CHANGE: Get character details for precise positioning ---
                    page_chars = page.chars
                    page_text_full = page.extract_text() or "" # Still useful for finding text content
                    
                    # Find categories and their precise Y coordinates using character details
                    categories_with_y = self._find_categories_by_char_location(page_chars)
                    
                    # Find tables and their bounding boxes
                    all_tables_detailed = page.find_tables() # This gives us the bbox for each table
                    # Also get the simple table data for extraction
                    simple_tables_data = page.extract_tables()

                    if not all_tables_detailed:
                        self.logger.debug(f"Page {page_num}: No tables detected by find_tables()")
                        continue

                    self.logger.debug(f"\nPage {page_num}: Found {len(all_tables_detailed)} table(s) by find_tables()")
                    self.logger.debug(f"Categories found on page with Y-coords: {[(c, round(y, 2)) for c, y in categories_with_y]}")

                    # Process each table based on its position
                    for table_idx, (table_obj, table_data) in enumerate(zip(all_tables_detailed, simple_tables_data)):
                         table_bbox = table_obj.bbox # (x0, y0, x1, y1) - y0 is top, y1 is bottom
                         table_top_y = table_bbox[1] # Top Y coordinate of the table
                        
                         # Determine the category for the current table based on its Y position
                         current_category = self._get_category_for_table_y_coordinate(table_top_y, categories_with_y)
                         
                         # If no category was found for this table's Y position, use the previous one
                         if current_category == "Unknown":
                             current_category = self.previous_category
                             self.logger.debug(f"      No preceding category found for table {table_idx+1}, using previous category: {current_category}")
                        
                         # Update the previous category for the next table or page
                         self.previous_category = current_category
                        
                         self.logger.debug(f"   Table {table_idx+1} bbox: ({round(table_bbox[0], 2)}, {round(table_bbox[1], 2)}, {round(table_bbox[2], 2)}, {round(table_bbox[3], 2)}), top_y={round(table_top_y, 2)}, determined category: {current_category}")
                        
                         # Extract VINs from the table data using the determined category
                         count = self._extract_vins_from_table(
                              table_data, date, f_out, page_num, table_idx+1, current_category
                         )
                         vins_extracted += count

            return vins_extracted

        except Exception as e:
            self.logger.error(f"Error processing {pdf_path.name}: {e}", exc_info=True)
            return 0
            
    def _find_categories_by_char_location(self, page_chars: List[Dict]) -> List[Tuple[str, float]]:
        """Find categories and their Y coordinates using character locations."""
        # Join all character text to search for categories
        full_text = "".join(char["text"] for char in page_chars)
        
        found_categories = []
        for category in CATEGORIES:
             # Find all occurrences of the category
             for match in re.finditer(re.escape(category), full_text, re.IGNORECASE):
                  start_pos = match.start()
                  
                  # Find the corresponding character object in page_chars
                  # This assumes character order matches text order closely enough for Y-coord lookup
                  # A more robust method might involve checking x/y ranges, but this is often sufficient.
                  if start_pos < len(page_chars):
                      y_coord = page_chars[start_pos]['top']
                      found_categories.append((category, y_coord))
                      self.logger.debug(f"       Found category '{category}' at char pos {start_pos}, approx Y={y_coord:.2f}")
        
        # Sort by Y coordinate (ascending, so top of page first)
        found_categories.sort(key=lambda x: x[1])
        return found_categories


    def _get_category_for_table_y_coordinate(self, table_top_y: float, categories_with_y: List[Tuple[str, float]]) -> str:
        """Determine the category for a table based on its Y coordinate."""
        # Find the category with the highest Y coordinate that is still less than the table's top Y coordinate
        applicable_category = "Unknown"
        for cat, cat_y in reversed(categories_with_y): # Iterate backwards (bottom categories first)
             if cat_y < table_top_y:
                  applicable_category = cat
                  break # Found the last category before this table
        
        return applicable_category


    def _extract_vins_from_table(
        self, table: List[List], date: str, f_out, page_num: int, table_idx: int, category: str # Added category parameter
    ) -> int:
        """Extract VINs from a single table using the provided category"""
        if not table or len(table) < 2:
            return 0

        # Strategy 1: Try to find header row
        header_info = self._find_header(table)

        if header_info:
            return self._extract_with_header(
                 table, header_info, date, f_out, page_num, table_idx, category # Pass category
            )
        else:
            # Strategy 2: Heuristic extraction (no clear headers)
            return self._extract_heuristic(
                table, date, f_out, page_num, table_idx, category # Pass category
            )

    def _find_header(self, table: List[List]) -> Optional[Dict]:
        """Locate header row and map column indices"""
        for row_idx, row in enumerate(table):
            if not row:
                continue

            # Convert row to searchable text
            row_text = ' '.join([str(cell).lower() for cell in row if cell])
            row_text = row_text.replace('\n', ' ')

            # Header must contain VIN-like identifier
            if not any(kw in row_text for kw in [' vin', 'n°', 'livr']):
                continue

            # Map columns
            col_map = {'row_idx': row_idx}

            for col_idx, cell in enumerate(row):
                if not cell:
                    continue

                cell_text = str(cell).lower().replace('\n', ' ')

                if 'vin' in cell_text or 'n°' in cell_text or 'livr' in cell_text:
                    col_map['vin'] = col_idx
                elif 'modèle' in cell_text or 'modele' in cell_text:
                    col_map['model'] = col_idx
                elif 'client' in cell_text:
                    col_map['client'] = col_idx

            # Valid header must have VIN column
            if 'vin' in col_map:
                self.logger.debug(f"   Header found at row {row_idx}: {col_map}")
                return col_map

        return None

    def _extract_with_header(
        self, table: List[List], header_info: Dict, date: str,
        f_out, page_num: int, table_idx: int, category: str # Added category
    ) -> int:
        """Extract VINs using known column positions"""
        header_row = header_info['row_idx']
        vin_col = header_info['vin']
        model_col = header_info.get('model', -1)
        client_col = header_info.get('client', -1)

        count = 0

        # Process data rows (after header)
        for row in table[header_row + 1:]:
            if not row or len(row) <= vin_col:
                continue

            # Extract VIN
            vin = self._clean_cell(row[vin_col])

            if not self._is_valid_vin(vin):
                continue

            # Extract Model
            model = ""
            if model_col >= 0 and model_col < len(row):
                model = self._clean_cell(row[model_col])

            # Extract Client
            client = ""
            if client_col >= 0 and client_col < len(row):
                client = self._clean_cell(row[client_col])
                client = self._clean_client(client)

            # Write output with determined category, but only if it's in the output list
            if category in OUTPUT_CATEGORIES:
                f_out.write(f"{date},{vin},{model},{client},{category}\n")
                count += 1
                self.logger.debug(f"      [{count}] {vin} | {model} | {client} | {category}")
            else:
                self.logger.debug(f"      Skipping VIN {vin} - category '{category}' not in OUTPUT_CATEGORIES")

        if count > 0:
            self.logger.debug(f"   Table {table_idx}: Extracted {count} VINs (with header) under category '{category}'")

        return count

    def _extract_heuristic(
        self, table: List[List], date: str, f_out, page_num: int, table_idx: int, category: str # Added category
    ) -> int:
        """Extract VINs without clear headers - search for VIN patterns"""
        count = 0

        for row in table:
            if not row or len(row) < 2:
                continue

            # Search for VIN in first several columns
            vin = None
            vin_col = -1

            for col_idx in range(min(6, len(row))):
                cell = self._clean_cell(row[col_idx])

                if self._is_valid_vin(cell):
                    vin = cell
                    vin_col = col_idx
                    break

            if not vin:
                continue

            # Extract adjacent cells as Model and Client
            model = ""
            if vin_col + 1 < len(row):
                model = self._clean_cell(row[vin_col + 1])

            client = ""
            if vin_col + 2 < len(row):
                client = self._clean_cell(row[vin_col + 2])
                client = self._clean_client(client)

            # Write output with determined category, but only if it's in the output list
            if category in OUTPUT_CATEGORIES:
                f_out.write(f"{date},{vin},{model},{client},{category}\n")
                count += 1
                self.logger.debug(f"      [{count}] {vin} | {model} | {client} | {category} (heuristic)")
            else:
                self.logger.debug(f"      Skipping VIN {vin} - category '{category}' not in OUTPUT_CATEGORIES")

        if count > 0:
            self.logger.debug(f"   Table {table_idx}: Extracted {count} VINs (heuristic) under category '{category}'")

        return count

    def _is_valid_vin(self, text: str) -> bool:
        """Validate if text looks like a VIN"""
        if not text or len(text) < 5:
            return False

        # Remove common whitespace/formatting
        text = text.strip()

        # Reject time patterns (08h20, 12:00)
        if re.match(r'^\d{2}[h:]\d{2}', text):
            return False

        # Reject pure date patterns
        if re.match(r'^\d{2}/\d{2}', text):
            return False

        # Reject common header words
        if text.lower() in [
            'heure', 'type', 'rapide', 'normale', 'site',
            'livr', 'client', 'modèle', 'modele', 'vin'
        ]:
            return False

        # VIN must contain both letters AND numbers
        has_letter = bool(re.search(r'[A-Za-z]', text))
        has_number = bool(re.search(r'\d', text))

        if not (has_letter and has_number):
            return False

        # Additional validation: typical VIN pattern
        # Format: 1-3 letters, 5-7 digits, optional dash/letters
        if re.match(r'^[A-Z]{1,3}\d{5,7}', text, re.IGNORECASE):
             return True

        # Alternative: mixed alphanumeric (common in your data)
        if re.match(r'^[A-Z]\d[A-Z0-9\-]{5,}', text, re.IGNORECASE):
            return True

        return False

    def _clean_cell(self, cell) -> str:
        """Clean and normalize cell content"""
        if cell is None:
            return ""

        text = str(cell)
        # Replace newlines with spaces
        text = text.replace('\n', ' ')
        # Collapse multiple spaces
        text = re.sub(r'\s+', ' ', text)
        # Strip whitespace
        text = text.strip()

        return text

    def _clean_client(self, text: str) -> str:
        """Remove noise from client names"""
        if not text:
            return ""

        # Remove excluded words
        cleaned = CLIENT_CLEAN_PATTERN.sub('', text)

        # Remove standalone numbers (like "66 ", "19/01 ")
        cleaned = re.sub(r'\b\d{1,2}\b', '', cleaned)
        cleaned = re.sub(r'\d{2}/\d{2}', '', cleaned)

        # Collapse whitespace
        cleaned = re.sub(r'\s+', ' ', cleaned)

        # Remove trailing slashes
        cleaned = re.sub(r'\s*/\s*$', '', cleaned)

        return cleaned.strip()

    def _extract_date(self, first_page) -> str:
        """Extract and format date from PDF header"""
        text = first_page.extract_text() or ""

        # Find date line
        match = re.search(
            r"Liste des rendez-vous pour le\s+(.+?)(?:\n|$)",
            text,
            re.IGNORECASE
        )

        if not match:
            return "Unknown"

        raw_date = match.group(1).strip()

        # Parse French date to MM/DD/YYYY
        try:
            parts = raw_date.lower().split()
            day = month = year = ""

            for part in parts:
                if part in FRENCH_MONTHS:
                    month = FRENCH_MONTHS[part] # Fixed: Removed .strip() and trailing space issue
                elif part.isdigit():
                    if len(part) == 4:
                        year = part
                    elif int(part) <= 31:
                        day = part.zfill(2)

            if day and month and year:
                return f"{month}/{day}/{year}"
        except Exception as e: # More specific error handling
            self.logger.warning(f"Date parsing failed for '{raw_date}', returning 'Unknown'. Error: {e}")
            pass # Fall back to returning "Unknown"

        return "Unknown" # Always return the formatted date or "Unknown"

def main():
    """Entry point"""
    if len(sys.argv) < 2:
        print("Usage: python script.py <pdf_folder>")
        print("Example: python script.py ./pdfs")
        sys.exit(1)

    # Get folder path
    input_path = sys.argv[1]
    target_path = Path(input_path)

    # Handle relative paths
    if not target_path.is_absolute():
        target_path = ROOT_DIR / input_path

    # Validate
    if not target_path.exists():
        print(f"❌ Error: Folder not found: {target_path}")
        sys.exit(1)

    if not target_path.is_dir():
        print(f"❌ Error: Not a directory: {target_path}")
        sys.exit(1)

    # Run extraction
    extractor = VINExtractor(target_path)
    extractor.run()

if __name__ == "__main__":
    main()