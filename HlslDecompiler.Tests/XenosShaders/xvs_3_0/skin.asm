xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=5
config VsResource=1
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 3, 0, 0, 0
dcl_index r0.x
/*    0.0 */       exec
/*    4   */          vfetch r5, r0.x, position0
/*    5   */          vfetch r1, r0.x, blendweight0
/*    6   */          vfetch r0, r0.x, blendindices0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    7   */          trunc r0, r0.wxyz
/*    8   */          mul r2, r0, c255.xxxx
/*    9   */          sge r0.___w, r_abs[0].xxxx, c255.yyyy
              +       maxas r0._, r2.xx
/*   10   */          dp4 r2.x___, r5.wzxy, c[2+a0].wzxy
/*   11   */          dp4 r3.x___, r5.wzxy, c[1+a0].wzxy
/*   12   */          dp4 r4.x___, r5.wzxy, c[0+a0].wzxy
              +       maxas r0._, r2.yy
/*    1.1 */       exec
/*   13   */          dp4 r2._y__, r5.wzxy, c[2+a0].wzxy
/*   14   */          dp4 r3._y__, r5.wzxy, c[1+a0].wzxy
/*   15   */          dp4 r4._y__, r5.wzxy, c[0+a0].wzxy
              +       maxas r0._, r2.zz
/*   16   */          dp4 r2.__z_, r5.wzxy, c[2+a0].wzxy
/*   17   */          dp4 r3.__z_, r5.wzxy, c[1+a0].wzxy
/*   18   */          dp4 r4.__z_, r5.wzxy, c[0+a0].wzxy
              +       maxas r0._, r2.ww
/*    2.0 */       exec
/*   19   */          dp4 r2.___w, r5.wzxy, c[2+a0].wzxy
/*   20   */          dp4 r3.___w, r5.wzxy, c[1+a0].wzxy
/*   21   */          dp4 r4.___w, r5.wzxy, c[0+a0].wzxy
/*   22   */          dp4 r0.x___, r4.xwzy, r1.wzyx
/*   23   */          dp4 r0._y__, r3.xwzy, r1.wzyx
/*   24   */          dp4 r0.__z_, r2.xwzy, r1.wzyx
/*    2.1 */       exec
/*   25   */          dp4 oPos.x___, c60.zxyw, r0.zxyw
/*   26   */          dp4 oPos._y__, c61.zxyw, r0.zxyw
/*   27   */          dp4 oPos.__z_, c62.zxyw, r0.zxyw
/*   28   */          dp4 oPos.___w, c63.zxyw, r0.zxyw
/*    3.0 */       alloc interpolators
/*    3.1 */       exece
