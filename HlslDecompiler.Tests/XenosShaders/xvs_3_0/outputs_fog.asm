xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=3
config VsResource=1
dcl_index r0.x
dcl_texcoord2 o0.xyz
dcl_color1 o1
dcl_fog0 o2.x
/*    0.0 */       exec
/*    3   */          vfetch r3, r0.x, position0
/*    4   */          vfetch r1.xyz_, r0.x, normal0
/*    5   */          vfetch r2, r0.x, color0
/*    6   */          vfetch r0.xy__, r0.x, texcoord3
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    7   */          dp4 oPos.x___, r3.wzxy, c0.wzxy
/*    8   */          dp4 oPos._y__, r3.wzxy, c1.wzxy
/*    9   */          dp4 oPos.__z_, r3.wzxy, c2.wzxy
/*   10   */          dp4 oPos.___w, r3.wzxy, c3.wzxy
/*    1.1 */       alloc interpolators
/*    2.0 */       exece
/*   11   */          maxs o2.x___, r0.yy
/*   12   */          max o1, r2, r2
/*   13   */          mul o0.xyz_, r1.xyzz, r0.xxxx
/*    2.1 */       cnop
