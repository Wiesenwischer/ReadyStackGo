ReadyStackGo PRTG Bundle
========================

Generated:   {{generatedAt}}
Source:      {{sourceHost}}
Root OID:    {{rootOid}}
RSGO ver:    {{rsgoVersion}}

Contents
--------

devicetemplates/readystackgo.template
    PRTG Device Template — describes the sensors (system scalars, environment
    table, product/stack/service tables) that PRTG will create on a
    ReadyStackGo host when Auto-Discovery is started.

snmplibs/READYSTACKGO-MIB.txt
    SNMPv2-SMI MIB file. Imported via the Paessler MIB Importer tool so PRTG
    can resolve OIDs to symbolic names in dashboards and lookups.

lookups/custom/*.ovl
    PRTG Value Lookups. Map the numeric enum values reported by the agent
    (e.g. productStatus=4) to a state + display text (e.g. "Failed", Error)
    so PRTG sensors light up red/yellow/green correctly.


Installation
------------

1. Stop the PRTG Probe service (or use the PRTG web UI to reload device
   templates / lookups after the files are in place — Tools menu).

2. Unpack this archive **into the PRTG install directory**:

        C:\Program Files (x86)\PRTG Network Monitor\

   The archive's folder layout mirrors PRTG's so files land in the correct
   subdirectories automatically.

3. Import the MIB into PRTG via the Paessler MIB Importer:

        File -> Import MIB File... -> snmplibs\READYSTACKGO-MIB.txt
        File -> Save for PRTG -> default folder

4. Start the PRTG Probe service again.

5. In the PRTG web UI:
   - Open the Device that represents your ReadyStackGo host.
   - Make sure an SNMP credential is set (community for v2c, USM user for v3).
   - Right-click -> Auto-Discovery (with template).
   - In the wizard, pick "ReadyStackGo Deployment".

6. Watch the new sensors appear and start polling on a 60 s interval.


Updating
--------

When ReadyStackGo's OID layout or status enums change, simply re-download
the bundle from the SNMP Monitoring settings page and overwrite the files
in the PRTG install directory. Existing sensors continue to work; new
columns become available on the next discovery run.


Troubleshooting
---------------

- Sensors show "No such name" -- the device template references an OID not
  served by this RSGO instance. Confirm SNMP is enabled in the RSGO
  settings page and that the Root OID in the bundle matches what the agent
  advertises (see {{rootOid}} above).

- Sensors show raw integer values (e.g. "Status = 4") instead of names
  ("Failed") -- the lookups were not imported. Check the
  Lookups\Custom\ folder under the PRTG install directory.

- Sensor counts on tables are stuck at zero -- the SNMP credentials are
  wrong or the agent is unreachable. Try a Sensor Test in PRTG first.


Read more
---------

ReadyStackGo SNMP documentation:
    https://readystackgo.dev/de/docs/monitoring/snmp/

PRTG device template authoring guide (Paessler):
    https://www.paessler.com/manuals/prtg/define_lookups
