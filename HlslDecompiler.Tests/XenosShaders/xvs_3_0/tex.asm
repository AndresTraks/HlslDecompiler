xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=1
config VsResource=1
dcl_index r0.x
/*    0.0 */       exec
/*    3   */          vfetch r1, r0.x, position0
/*    4   */          vfetch r0.xy__, r0.x, texcoord0
/*    5   */          serialize
                      tfetch2D r0, r0.xyx, tf16, UseComputedLOD=false
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    6   */          add oPos, r1, r0
/*    1.1 */       alloc interpolators
/*    2.0 */       exece
/*    2.1 */       cnop
