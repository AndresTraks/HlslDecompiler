xvs_3_0
config AutoSerialize=false
config AutoResource=false
config VsMaxReg=0
config VsResource=1
dcl_index r0.x
/*    0.0 */       exec
/*    5   */          vfetch r0.wzyx, r0.x, position0
/*    6   */          serialize
                      max r0._, c0, c0
/*    0.1 */       cjmp !b0, L4
/*    1.0 */       exec
/*    7   */          mul r0, r0.wzyx, c0
/*    1.1 */       jmp L5
                label L4
/*    2.0 */       exec
/*    8   */          add r0, r0.wzyx, c0
                label L5
/*    2.1 */       alloc position
/*    3.0 */       exec
/*    9   */          max oPos, r0, r0
/*    3.1 */       alloc interpolators
/*    4.0 */       exece
/*    4.1 */       cnop
