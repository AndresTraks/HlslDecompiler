xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=2
config VsResource=1
dcl_index r0.x
/*    0.0 */       exec
/*    3   */          vfetch r2, r0.x, position0
/*    4   */          vfetch r0.xy__, r0.x, texcoord0
/*    0.1 */       alloc position
/*    1.0 */       exec
/*    5   */          trunc r1.xy__, r0.xyyy
/*    6   */          maxas r0._, r1.xx
/*    7   */          mul r0, r2, c[0+a0]
              +       maxas r0._, r1.yy
/*    8   */          add oPos, r0, c[0+a0]
/*    1.1 */       alloc interpolators
/*    2.0 */       exece
/*    2.1 */       cnop
