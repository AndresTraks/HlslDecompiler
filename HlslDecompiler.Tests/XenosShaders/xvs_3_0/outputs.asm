xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=2
config VsResource=1
config VsExportMode=sprite
dcl_index r0.x
dcl_texcoord0 o0
dcl_texcoord5 o1.xy
dcl_color0 o2
dcl_color1 o3
dcl_fog0 o4.x
/*    0.0 */       exec
/*    3   */          vfetch r2, r0.x, position0
/*    4   */          vfetch r1, r0.x, color0
/*    5   */          vfetch r0.xyz1, r0.x, normal0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    6   */          maxs oPts.x___, r0.zz
/*    7   */          max oPos, r2, r2
/*    1.1 */       alloc interpolators
/*    2.0 */       exece
/*    8   */          maxs o4.x___, r2.ww
/*    9   */          max o1.xy__, r0.xyyy, r0.xyyy
/*   10   */          max o2, r1, r1
/*   11   */          max o3, r1.wzyx, r1.wzyx
/*   12   */          max o0, r0, r0
/*    2.1 */       cnop
