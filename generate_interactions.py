#!/usr/bin/env python3
"""Print selected interaction elements and description.

This script no longer writes specification files. The Unity editor window
passes a natural language description followed by the names of selected
interaction elements as command line arguments. The values are exposed as
top-level variables and printed for downstream processing.
"""

import sys


description = sys.argv[1] if len(sys.argv) > 1 else ""
interaction_elements = sys.argv[2:]


print("interaction_elements:", interaction_elements)
print("description:", description)

