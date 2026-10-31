xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=2
config VsResource=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 0.01, 0, 0, 0
dcl_index r0.x
dcl_texcoord0 o0.xyz
dcl_fog0 o1.x
/*    0.0 */       exec
/*    3   */          vfetch r2, r0.x, position0
/*    4   */          vfetch r0.xyz_, r0.x, normal0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    5   */          dp4 oPos.x___, r2.wzxy, c0.wzxy
/*    6   */          dp4 oPos._y__, r2.wzxy, c1.wzxy
/*    7   */          dp4 oPos.__z_, r2.wzxy, c2.wzxy
/*    8   */          dp4 oPos.___w, r2.wzxy, c3.wzxy
/*    1.1 */       alloc interpolators
/*    2.0 */       exec
/*    9   */          dp3 r1.x___, r0.zxyy, c4.zxyy
/*   10   */          dp3 r1._y__, r0.zxyy, c5.zxyy
/*   11   */          dp3 r1.__z_, r0.zxyy, c6.zxyy
/*   12   */          dp4 r0.x___, r2.wzxy, c4.wzxy
/*   13   */          dp4 r0._y__, r2.wzxy, c5.wzxy
/*   14   */          dp4 r0.__z_, r2.wzxy, c6.wzxy
/*    2.1 */       exece
/*   15   */          add r0.x_zw, r0.xyyz, -c7.xyyz
/*   16   */          dp3 r0._y__, r1.zxyy, r1.zxyy
/*   17   */          dp3 r0.x___, r0.wxzz, r0.wxzz
              +       rsq r0._y__, r_abs[0].y
/*   18   */          mul o0.xyz_, r1.xyzz, r0.yyyy
/*   19   */          sqrt r0._y__, r_abs[0].x
/*   20   */          mulsc_sat o1.x___, c255.x, r0.y
