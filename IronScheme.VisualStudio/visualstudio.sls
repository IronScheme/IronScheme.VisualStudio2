﻿; License
; Copyright (c) 2016-2021 Llewellyn Pritchard
; All rights reserved.
; This source code is subject to terms and conditions of the BSD License.

(library (visualstudio)
  (export
    parse-repl 
    run-expansion
    read-file
    get-forms
    read-imports
    get-error-location)
  (import 
    (ironscheme)
    (ironscheme reader))

  (define (parse-repl text)
    (let ((port (open-string-input-port
                  (string-append "(begin "
                                 text
                                 "\n)"))))
      (let ((expr (read port))) 
        (if (eof-object? expr)
            #f 
            expr)))) 
 
  (define (read-definitions subst env invoke-code exp*)
    (let ((lookup (make-eq-hashtable))
          (bindings (make-eq-hashtable)))
      (for-each (lambda (x)
                  (hashtable-set! lookup (cdr x) (car x)))
                subst)
      (for-each (lambda (x)
                  (let ((type (cadr x)))
                    (case type
                      [(global global-macro $rtd) 
                        (hashtable-set! bindings 
                                        (hashtable-ref lookup (car x) #f)
                                        type)])))
                env)
      (let-values (((k v) (hashtable-entries bindings)))
        (values k v (expanded->core invoke-code) exp*))))

  (define (run-expansion e)
    (call-with-values 
      (lambda ()
        (if (null? (cdr e))
            (let-values (((name exp* imp* b*) (parse-library (car e))))
              (let-values (((lib* invoke-code macro* export-subst export-env)
                            (top-level-expander (cons (cons 'import imp*) b*))))
                (values export-subst export-env invoke-code exp*)))
            (let-values (((lib* invoke-code macro* export-subst export-env) (top-level-expander e)))
              (values export-subst export-env invoke-code '()))))
      read-definitions))

  (define (read-file port)
    (let f ((a '()))
      (let ((e (read-annotated port)))
        (if (eof-object? e)
            (reverse a)
            (f (cons e a))))))

  (define (get-location con)
    (let ((rtd (record-rtd con)))
      (if (eq? (record-type-name rtd) '&source-information)
          ((record-accessor rtd 1) con)
          #f)))

  (define (condition-location conds)
    (exists get-location (simple-conditions conds)))

  (define (get-error-location conds)
    (condition-location conds))

  (define (get-forms proc name)
    (let-values (((p e) (open-string-output-port)))
      (for-each (lambda (f)
                  (fprintf p "~a\n" (cons (string->symbol name) (cdr f))))
                (call-with-values (lambda () (procedure-form proc)) list))
      (e)))
            
  (define (read-imports content)
    (if (null? (cdr content))
        (let-values (((name exp* imp* b*) (parse-library (car content))))
          imp*)
        (let-values (((imp* b*) (parse-top-level-program content)))
          imp*)))

  (allow-library-redefinition #t)
  (debug-mode? #t))
