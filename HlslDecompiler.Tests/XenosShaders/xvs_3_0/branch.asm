xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=2
config VsResource=1
dcl_index r0.x
/*    0.0 */       exec
/*    4   */          vfetch r2, r0.x, position0
/*    5   */          vfetch r1.zyx_, r0.x, normal0
/*    6   */          serialize
                      dp4 r0.__z_, r2.wzxy, c0.wzxy
/*    7   */          dp4 r0._y__, r2.wzxy, c1.wzxy
/*    8   */          dp4 r0.x___, r2.wzxy, c2.wzxy
/*    9   */          dp4 r0.___w, r2.wzxy, c3.wzxy
/*    0.1 */       cjmp !b0, L3
/*    1.0 */       exec
/*   10   */          add r0.xyz_, r0.xyzz, r1.xyzz
                label L3
/*    1.1 */       alloc position
/*    2.0 */       exec
/*   11   */          max oPos, r0.zyxw, r0.zyxw
/*    2.1 */       alloc interpolators
/*    3.0 */       exece
/*    3.1 */       cnop
