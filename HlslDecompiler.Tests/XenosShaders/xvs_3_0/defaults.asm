xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=1
config VsResource=1
dcl_index r0.x
dcl_texcoord0 o0.xy
dcl_color0 o1
/*    0.0 */       exec
/*    3   */          vfetch r1, r0.x, position0
/*    4   */          vfetch r0.xy__, r0.x, texcoord0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    5   */          dp4 oPos.x___, r1.wzxy, c0.wzxy
/*    6   */          dp4 oPos._y__, r1.wzxy, c1.wzxy
/*    7   */          dp4 oPos.__z_, r1.wzxy, c2.wzxy
/*    8   */          dp4 oPos.___w, r1.wzxy, c3.wzxy
/*    1.1 */       alloc interpolators
/*    2.0 */       exece
/*    9   */          max o0.xy__, r0.xyyy, r0.xyyy
/*   10   */          add o1, c4, c4
/*    2.1 */       cnop
