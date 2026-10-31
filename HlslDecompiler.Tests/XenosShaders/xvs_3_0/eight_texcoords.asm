xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=1
config VsResource=1
dcl_index r0.x
dcl_texcoord0 o0
dcl_texcoord1 o1
dcl_texcoord2 o2
dcl_texcoord3 o3
dcl_texcoord4 o4
dcl_texcoord5 o5
dcl_texcoord6 o6
dcl_texcoord7 o7
/*    0.0 */       exec
/*    3   */          vfetch r1, r0.x, position0
/*    4   */          vfetch r0, r0.x, color0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    5   */          max oPos, r1, r1
/*    1.1 */       alloc interpolators
/*    2.0 */       exec
/*    6   */          mul o6.x___, r1.xxxx, r0.xxxx
/*    7   */          max o0, r0, r0
/*    8   */          add o1, r0, r0
/*    9   */          max o2, r0.wzyx, r0.wzyx
/*   10   */          max o5, r1.yzwx, r1.yzwx
/*   11   */          frc o7, r1
/*    2.1 */       exece
/*   12   */          mul o6._yzw, r0.yyzw, r1.xxxx
/*   13   */          mul o3, r1, r0
/*   14   */          add o4, r1, r0
