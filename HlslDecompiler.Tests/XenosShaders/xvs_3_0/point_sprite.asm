xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=2
config VsResource=1
config VsExportMode=sprite
dcl_index r0.x
dcl_color0 o0
/*    0.0 */       exec
/*    3   */          vfetch r2, r0.x, position0
/*    4   */          vfetch r1, r0.x, color0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    5   */          max oPos, r2, r2
/*    6   */          rcp r0._y__, r2.w
/*    7   */          mulsc oPts.x___, c0.x, r0.y
/*    1.1 */       alloc interpolators
/*    2.0 */       exece
/*    8   */          max o0, r1, r1
/*    2.1 */       cnop
